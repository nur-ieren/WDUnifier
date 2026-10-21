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

        private Vector2 scroll;
        private readonly StringBuilder logBuilder = new StringBuilder();

        [MenuItem("nur_ieren/WDUnifier")]
        private static void ShowWindow()
        {
            var window = GetWindow<WDUnifier>("Write Defaults 一括切り替え");
            window.minSize = new Vector2(460, 480);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("対象の指定方法", EditorStyles.boldLabel);
            targetMode = (TargetMode)GUILayout.Toolbar(
                (int)targetMode,
                new[] { "ファイル指定", "GameObject指定" });

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
            EditorGUILayout.LabelField("Write Defaults の設定値", EditorStyles.boldLabel);
            writeDefaultsValue = GUILayout.Toolbar(writeDefaultsValue ? 1 : 0, new[] { "OFF にする (0 / false)", "ON にする (1 / true)" }) == 1;

            verboseLog = EditorGUILayout.Toggle(
                new GUIContent("詳細ログを出力する", "対象ステートが多い場合はOFFにすると高速化します"),
                verboseLog);

            EditorGUILayout.Space();

            bool hasTarget = targetMode == TargetMode.File
                ? targetControllers.Count > 0
                : targetGameObject != null;

            using (new EditorGUI.DisabledScope(!hasTarget))
            {
                if (GUILayout.Button("プレビュー（対象と変更件数を確認）", GUILayout.Height(28)))
                {
                    Preview();
                }

                var prevColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("一括切り替えを実行", GUILayout.Height(32)))
                {
                    if (EditorUtility.DisplayDialog(
                        "確認",
                        $"対象の Animator Controller 内の全ステートの Write Defaults を " +
                        $"{(writeDefaultsValue ? "ON (true)" : "OFF (false)")} に揃えます。\n" +
                        "Ctrl+Z (Undo) で戻せますが、念のためプロジェクトのバックアップを推奨します。\n\n実行しますか？",
                        "実行する", "キャンセル"))
                    {
                        Execute();
                    }
                }
                GUI.backgroundColor = prevColor;
            }

            if (!hasTarget)
            {
                EditorGUILayout.HelpBox("対象（ファイル または GameObject）を1つ以上指定してください。", MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("ログ", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(180));
            EditorGUILayout.TextArea(logBuilder.ToString(), GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        // ============================================================
        // GUI - ファイル指定モード
        // ============================================================
        private void DrawFileModeGUI()
        {
            EditorGUILayout.LabelField("対象の Animator Controller", EditorStyles.boldLabel);

            Rect dropArea = GUILayoutUtility.GetRect(0, 46, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "ここに Animator Controller をドラッグ＆ドロップ\n(複数選択してまとめてドロップ可)", EditorStyles.helpBox);
            HandleDragAndDrop(dropArea);

            EditorGUILayout.BeginHorizontal();
            fileToAdd = (AnimatorController)EditorGUILayout.ObjectField(fileToAdd, typeof(AnimatorController), false);
            using (new EditorGUI.DisabledScope(fileToAdd == null))
            {
                if (GUILayout.Button("追加", GUILayout.Width(50)))
                {
                    AddControllerToList(fileToAdd);
                    fileToAdd = null;
                }
            }
            EditorGUILayout.EndHorizontal();

            if (targetControllers.Count > 0)
            {
                EditorGUILayout.LabelField($"登録済み: {targetControllers.Count} 件");
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
                if (GUILayout.Button("リストを全てクリア"))
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
                new GUIContent("対象GameObject", "このオブジェクト配下で使われている Animator Controller が対象になります"),
                targetGameObject, typeof(GameObject), true);

            includeInactive = EditorGUILayout.Toggle(
                new GUIContent("非アクティブなオブジェクトも含める", "OFFにすると非アクティブな子オブジェクトは検索対象から除外します"),
                includeInactive);

            EditorGUILayout.HelpBox(
                "Animatorコンポーネントだけでなく、Modular Avatarの Merge Animator など" +
                " NDMF系ツールが独自コンポーネント内に保持している Animator Controller の参照も" +
                "配下の全コンポーネントを走査して検出します。",
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

            logBuilder.AppendLine($"対象 Animator Controller: {controllers.Count} 件");

            int totalStates = 0;
            int willChange = 0;

            foreach (var c in controllers)
            {
                int before = willChange;
                foreach (var layer in c.layers)
                {
                    CountStates(layer.stateMachine, writeDefaultsValue, ref totalStates, ref willChange);
                }
                logBuilder.AppendLine($" - {AssetDatabase.GetAssetPath(c)} (変更対象 {willChange - before} 件)");
            }

            logBuilder.AppendLine("----------------------------");
            logBuilder.AppendLine($"総ステート数: {totalStates} / うち変更が必要: {willChange}");

            if (controllers.Count == 0)
            {
                if (targetMode == TargetMode.File)
                {
                    logBuilder.AppendLine("ファイルが登録されていません。");
                }
                else
                {
                    logBuilder.AppendLine("対象GameObject配下に Animator Controller への参照が見つかりませんでした。");
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
                logBuilder.AppendLine("対象の Animator Controller が見つかりませんでした。");
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
                        "Write Defaults 一括処理中...",
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
                        logBuilder.AppendLine($"OK ({changedInThisController}ステート変更): {AssetDatabase.GetAssetPath(controller)}");
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
                logBuilder.AppendLine($"キャンセルされました。途中まで {totalChanged} ステートを変更済みです。");
            }
            else
            {
                logBuilder.AppendLine($"完了：{controllers.Count} 個の Controller、合計 {totalChanged} ステートの Write Defaults を " +
                    $"{(writeDefaultsValue ? "ON" : "OFF")} に変更しました。");
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
