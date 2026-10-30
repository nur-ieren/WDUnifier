// WDUnifier.cs
// 配置場所: Assets/nur_ieren/ 配下(Editorフォルダ不要。全体を #if UNITY_EDITOR で囲んでいます)
// 対応バージョン: Unity 2022.3.22f1
//
// AnimatorController内の全ステートの Write Defaults (m_WriteDefaultValues) を
// 一括でON/OFFに揃えるツールです。テキストエディタでYAMLを直接書き換える代わりに、
// AnimatorController の公式API経由で安全に書き換えます。
//
// 対象の指定方法は2種類:
//   ・ファイル指定    : Animator Controller アセットを直接ドラッグ&ドロップ／追加（複数可）
//   ・GameObject指定  : 指定GameObject配下から使われている Animator Controller を自動検出
//
// GameObject指定モードでは、Animatorコンポーネントの runtimeAnimatorController だけでなく、
// Modular Avatar の Merge Animator など、NDMF系ツールが独自コンポーネント内に保持している
// AnimatorController参照も取りこぼさないよう、配下の全コンポーネントを SerializedObject 経由で
// リフレクション走査して検出します（特定ツールのクラスに依存しないため、未知のNDMF拡張にも対応）。
//
// 使い方:
//   Unityメニュー > nur_ieren > WDUnifier
//   ウィンドウ上部の「日本語 / English」で表示言語を切り替えられます（選択は EditorPrefs に保存されます）。
//
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace nur_ieren
{
    public class WDUnifier : EditorWindow
    {
        private enum TargetMode
        {
            File,
            GameObjectHierarchy
        }

        private TargetMode targetMode = TargetMode.File;

        // --- ファイル指定モード ---
        private readonly List<AnimatorController> targetControllers = new List<AnimatorController>();
        private AnimatorController fileToAdd;

        // --- GameObject指定モード ---
        private GameObject targetGameObject;
        private bool includeInactive = true;

        // --- 共通設定 ---
        private bool writeDefaultsValue = true; // true = ON (1) / false = OFF (0)
        private bool verboseLog = true;

        // --- 表示言語 / Display language ---
        private const string LangPrefKey = "nur_ieren.WDUnifier.English";
        private bool isEnglish;

        private Vector2 scroll;
        private readonly StringBuilder logBuilder = new StringBuilder();

        [MenuItem("nur_ieren/WDUnifier")]
        private static void ShowWindow()
        {
            var window = GetWindow<WDUnifier>();
            window.UpdateWindowTitle();
            window.minSize = new Vector2(460, 480);
        }

        private void OnEnable()
        {
            isEnglish = EditorPrefs.GetBool(LangPrefKey, false);
            UpdateWindowTitle();
        }

        // 表示言語に応じた文字列を返す / Returns the string for the current display language
        private string T(string ja, string en)
        {
            return isEnglish ? en : ja;
        }

        private void UpdateWindowTitle()
        {
            titleContent = new GUIContent(T("Write Defaults 一括切り替え", "Write Defaults Batch Switcher"));
        }

        // ウィンドウ上部の言語切り替え (日本語 / English)
        private void DrawLanguageToggle()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            int selected = GUILayout.Toolbar(isEnglish ? 1 : 0, new[] { "日本語", "English" }, GUILayout.Width(160));
            EditorGUILayout.EndHorizontal();

            bool newIsEnglish = selected == 1;
            if (newIsEnglish != isEnglish)
            {
                isEnglish = newIsEnglish;
                EditorPrefs.SetBool(LangPrefKey, isEnglish);
                UpdateWindowTitle();
            }

            EditorGUILayout.Space();
        }

        private void OnGUI()
        {
            DrawLanguageToggle();

            EditorGUILayout.LabelField(T("対象の指定方法", "Target Selection Mode"), EditorStyles.boldLabel);
            targetMode = (TargetMode)GUILayout.Toolbar(
                (int)targetMode,
                new[] { T("ファイル指定", "File"), T("GameObject指定", "GameObject") });

            EditorGUILayout.Space();

            if (targetMode == TargetMode.File)
            {
                DrawFileModeGUI();
            }
            else
            {
                DrawGameObjectModeGUI();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(T("Write Defaults の設定値", "Write Defaults Value"), EditorStyles.boldLabel);
            writeDefaultsValue = GUILayout.Toolbar(writeDefaultsValue ? 1 : 0, new[] { T("OFF にする (0 / false)", "Set OFF (0 / false)"), T("ON にする (1 / true)", "Set ON (1 / true)") }) == 1;

            verboseLog = EditorGUILayout.Toggle(
                new GUIContent(T("詳細ログを出力する", "Verbose log"), T("対象ステートが多い場合はOFFにすると高速化します", "Turn OFF to speed up when there are many states")),
                verboseLog);

            EditorGUILayout.Space();

            bool hasTarget = targetMode == TargetMode.File
                ? targetControllers.Count > 0
                : targetGameObject != null;

            using (new EditorGUI.DisabledScope(!hasTarget))
            {
                if (GUILayout.Button(T("プレビュー（対象と変更件数を確認）", "Preview (check targets and change count)"), GUILayout.Height(28)))
                {
                    Preview();
                }

                var prevColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button(T("一括切り替えを実行", "Run Batch Switch"), GUILayout.Height(32)))
                {
                    string wdLabel = writeDefaultsValue ? "ON (true)" : "OFF (false)";
                    if (EditorUtility.DisplayDialog(
                        T("確認", "Confirm"),
                        T($"対象の Animator Controller 内の全ステートの Write Defaults を {wdLabel} に揃えます。\n" +
                          "Ctrl+Z (Undo) で戻せますが、念のためプロジェクトのバックアップを推奨します。\n\n実行しますか？",
                          $"This will set Write Defaults of all states in the target Animator Controllers to {wdLabel}.\n" +
                          "You can undo with Ctrl+Z, but backing up your project first is recommended.\n\nProceed?"),
                        T("実行する", "Run"), T("キャンセル", "Cancel")))
                    {
                        Execute();
                    }
                }
                GUI.backgroundColor = prevColor;
            }

            if (!hasTarget)
            {
                EditorGUILayout.HelpBox(T("対象（ファイル または GameObject）を1つ以上指定してください。", "Specify at least one target (file or GameObject)."), MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(T("ログ", "Log"), EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(180));
            EditorGUILayout.TextArea(logBuilder.ToString(), GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        // ============================================================
        // GUI - ファイル指定モード
        // ============================================================
        private void DrawFileModeGUI()
        {
            EditorGUILayout.LabelField(T("対象の Animator Controller", "Target Animator Controllers"), EditorStyles.boldLabel);

            Rect dropArea = GUILayoutUtility.GetRect(0, 46, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, T("ここに Animator Controller をドラッグ＆ドロップ\n(複数選択してまとめてドロップ可)", "Drag & drop Animator Controllers here\n(multiple selection supported)"), EditorStyles.helpBox);
            HandleDragAndDrop(dropArea);

            EditorGUILayout.BeginHorizontal();
            fileToAdd = (AnimatorController)EditorGUILayout.ObjectField(fileToAdd, typeof(AnimatorController), false);
            using (new EditorGUI.DisabledScope(fileToAdd == null))
            {
                if (GUILayout.Button(T("追加", "Add"), GUILayout.Width(50)))
                {
                    AddControllerToList(fileToAdd);
                    fileToAdd = null;
                }
            }
            EditorGUILayout.EndHorizontal();

            if (targetControllers.Count > 0)
            {
                EditorGUILayout.LabelField(T($"登録済み: {targetControllers.Count} 件", $"Registered: {targetControllers.Count}"));
                for (int i = targetControllers.Count - 1; i >= 0; i--)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField(targetControllers[i], typeof(AnimatorController), false);
                    if (GUILayout.Button("×", GUILayout.Width(24)))
                    {
                        targetControllers.RemoveAt(i);
                    }
                    EditorGUILayout.EndHorizontal();
                }
                if (GUILayout.Button(T("リストを全てクリア", "Clear all")))
                {
                    targetControllers.Clear();
                }
            }
        }

        private void HandleDragAndDrop(Rect dropArea)
        {
            Event evt = Event.current;
            if (!dropArea.Contains(evt.mousePosition)) return;

            switch (evt.type)
            {
                case EventType.DragUpdated:
                    bool hasValid = System.Array.Exists(DragAndDrop.objectReferences, o => o is AnimatorController);
                    DragAndDrop.visualMode = hasValid ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                    evt.Use();
                    break;

                case EventType.DragPerform:
                    DragAndDrop.AcceptDrag();
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is AnimatorController ac)
                        {
                            AddControllerToList(ac);
                        }
                    }
                    evt.Use();
                    break;
            }
        }

        private void AddControllerToList(AnimatorController ac)
        {
            if (ac != null && !targetControllers.Contains(ac))
            {
                targetControllers.Add(ac);
            }
        }

        // ============================================================
        // GUI - GameObject指定モード
        // ============================================================
        private void DrawGameObjectModeGUI()
        {
            targetGameObject = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent(T("対象GameObject", "Target GameObject"), T("このオブジェクト配下で使われている Animator Controller が対象になります", "Animator Controllers used under this object are processed")),
                targetGameObject, typeof(GameObject), true);

            includeInactive = EditorGUILayout.Toggle(
                new GUIContent(T("非アクティブなオブジェクトも含める", "Include inactive objects"), T("OFFにすると非アクティブな子オブジェクトは検索対象から除外します", "When OFF, inactive child objects are excluded from the search")),
                includeInactive);

            EditorGUILayout.HelpBox(
                T("Animatorコンポーネントだけでなく、Modular Avatarの Merge Animator など" +
                  " NDMF系ツールが独自コンポーネント内に保持している Animator Controller の参照も" +
                  "配下の全コンポーネントを走査して検出します。",
                  "Detects not only the Animator component but also Animator Controller references held inside" +
                  " components of NDMF-based tools such as Modular Avatar's Merge Animator," +
                  " by scanning all components under the object."),
                MessageType.Info);
        }

        // ============================================================
        // 対象コントローラーの収集
        // ============================================================
        private HashSet<AnimatorController> CollectTargetControllers()
        {
            return targetMode == TargetMode.File
                ? CollectFromFileList()
                : CollectFromGameObject();
        }

        private HashSet<AnimatorController> CollectFromFileList()
        {
            var result = new HashSet<AnimatorController>();
            foreach (var c in targetControllers)
            {
                if (c != null) result.Add(c);
            }
            return result;
        }

        private HashSet<AnimatorController> CollectFromGameObject()
        {
            var result = new HashSet<AnimatorController>();
            if (targetGameObject == null) return result;

            // 1. Animator コンポーネント経由
            var animators = targetGameObject.GetComponentsInChildren<Animator>(includeInactive);
            foreach (var anim in animators)
            {
                var ac = ResolveToAnimatorController(anim.runtimeAnimatorController);
                if (ac != null) result.Add(ac);
            }

            // 2. それ以外の全コンポーネント（NDMF系ツールなど）をリフレクション走査
            //    特定のツールのクラスに依存しないよう、SerializedObjectで
            //    AnimatorController型/AnimatorOverrideController型の参照フィールドを総当たりで検出する
            var allComponents = targetGameObject.GetComponentsInChildren<Component>(includeInactive);
            foreach (var comp in allComponents)
            {
                if (comp == null) continue; // Missing Script
                if (comp is Animator) continue; // 1.で処理済み
                if (comp is Transform) continue; // 対象になり得ないので明確に除外（走査コスト削減）

                CollectAnimatorControllersFromComponent(comp, result);
            }

            return result;
        }

        private static void CollectAnimatorControllersFromComponent(Component comp, HashSet<AnimatorController> result)
        {
            SerializedObject so;
            try
            {
                so = new SerializedObject(comp);
            }
            catch
            {
                return; // シリアライズできないコンポーネントは無視
            }

            SerializedProperty prop = so.GetIterator();
            bool enterChildren = true;
            while (prop.Next(enterChildren))
            {
                enterChildren = true;

                if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;

                var obj = prop.objectReferenceValue;
                if (obj == null) continue;

                if (obj is AnimatorController ac)
                {
                    result.Add(ac);
                }
                else if (obj is AnimatorOverrideController aoc)
                {
                    var baseAc = ResolveToAnimatorController(aoc);
                    if (baseAc != null) result.Add(baseAc);
                }
            }
        }

        private static AnimatorController ResolveToAnimatorController(RuntimeAnimatorController rac)
        {
            // Override Controller が連鎖している場合も辿ってベースのAnimatorControllerまで解決する
            while (rac is AnimatorOverrideController overrideController)
            {
                rac = overrideController.runtimeAnimatorController;
            }
            return rac as AnimatorController;
        }

        // ============================================================
        // プレビュー
        // ============================================================
        private void Preview()
        {
            logBuilder.Clear();
            var controllers = CollectTargetControllers();

            logBuilder.AppendLine(T($"対象 Animator Controller: {controllers.Count} 件", $"Target Animator Controllers: {controllers.Count}"));

            int totalStates = 0;
            int willChange = 0;

            foreach (var c in controllers)
            {
                int before = willChange;
                foreach (var layer in c.layers)
                {
                    CountStates(layer.stateMachine, writeDefaultsValue, ref totalStates, ref willChange);
                }
                logBuilder.AppendLine(T($" - {AssetDatabase.GetAssetPath(c)} (変更対象 {willChange - before} 件)", $" - {AssetDatabase.GetAssetPath(c)} ({willChange - before} to change)"));
            }

            logBuilder.AppendLine("----------------------------");
            logBuilder.AppendLine(T($"総ステート数: {totalStates} / うち変更が必要: {willChange}", $"Total states: {totalStates} / needing change: {willChange}"));

            if (controllers.Count == 0)
            {
                if (targetMode == TargetMode.File)
                {
                    logBuilder.AppendLine(T("ファイルが登録されていません。", "No files registered."));
                }
                else
                {
                    logBuilder.AppendLine(T("対象GameObject配下に Animator Controller への参照が見つかりませんでした。", "No Animator Controller references were found under the target GameObject."));
                }
            }
        }

        private static void CountStates(AnimatorStateMachine sm, bool targetValue, ref int total, ref int willChange)
        {
            foreach (var cs in sm.states)
            {
                total++;
                if (cs.state.writeDefaultValues != targetValue) willChange++;
            }
            foreach (var csm in sm.stateMachines)
            {
                CountStates(csm.stateMachine, targetValue, ref total, ref willChange);
            }
        }

        // ============================================================
        // 実行
        // ============================================================
        private void Execute()
        {
            logBuilder.Clear();

            var controllers = CollectTargetControllers();
            if (controllers.Count == 0)
            {
                logBuilder.AppendLine(T("対象の Animator Controller が見つかりませんでした。", "No target Animator Controllers were found."));
                return;
            }

            int totalChanged = 0;
            int controllerIndex = 0;
            int controllerTotal = controllers.Count;
            bool canceled = false;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var controller in controllers)
                {
                    controllerIndex++;

                    if (EditorUtility.DisplayCancelableProgressBar(
                        T("Write Defaults 一括処理中...", "Processing Write Defaults..."),
                        $"{controllerIndex} / {controllerTotal}: {controller.name}",
                        (float)controllerIndex / controllerTotal))
                    {
                        canceled = true;
                        break;
                    }

                    int changedInThisController = 0;
                    foreach (var layer in controller.layers)
                    {
                        ApplyToStateMachine(layer.stateMachine, writeDefaultsValue, ref changedInThisController);
                    }

                    if (changedInThisController > 0)
                    {
                        EditorUtility.SetDirty(controller);
                    }

                    totalChanged += changedInThisController;

                    if (verboseLog)
                    {
                        logBuilder.AppendLine(T($"OK ({changedInThisController}ステート変更): {AssetDatabase.GetAssetPath(controller)}", $"OK ({changedInThisController} state(s) changed): {AssetDatabase.GetAssetPath(controller)}"));
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();

            logBuilder.AppendLine("----------------------------");
            if (canceled)
            {
                logBuilder.AppendLine(T($"キャンセルされました。途中まで {totalChanged} ステートを変更済みです。", $"Canceled. {totalChanged} state(s) were changed before cancellation."));
            }
            else
            {
                string wdLabel = writeDefaultsValue ? "ON" : "OFF";
                logBuilder.AppendLine(T(
                    $"完了：{controllers.Count} 個の Controller、合計 {totalChanged} ステートの Write Defaults を {wdLabel} に変更しました。",
                    $"Done: changed Write Defaults of {totalChanged} state(s) across {controllers.Count} controller(s) to {wdLabel}."));
            }
        }

        private static void ApplyToStateMachine(AnimatorStateMachine sm, bool targetValue, ref int changedCount)
        {
            foreach (var cs in sm.states)
            {
                var state = cs.state;
                if (state.writeDefaultValues != targetValue)
                {
                    Undo.RecordObject(state, "Write Defaults Batch");
                    state.writeDefaultValues = targetValue;
                    EditorUtility.SetDirty(state);
                    changedCount++;
                }
            }

            foreach (var csm in sm.stateMachines)
            {
                ApplyToStateMachine(csm.stateMachine, targetValue, ref changedCount);
            }
        }
    }
}
#endif
