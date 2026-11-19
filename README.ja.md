# WDUnifier

[English](README.md)

複数のAnimator Controllerに含まれるWrite Defaultsの設定を一括で切り替えるためのUnity Editor用ツールです。

## コンセプト

WDUnifierは、アバタープロジェクトにおけるWrite Defaultsの設定管理を簡略化するために開発されました。

複数のAnimator Controllerを使用する場合、各StateのWrite Defaults設定を1つずつ手動で変更するには時間がかかります。

このツールでは、複数のAnimator Controllerに含まれるすべてのStateのWrite Defaultsを、ONまたはOFFに一括変更できます。

また、シリアライズされたYAMLファイルを直接編集するのではなく、UnityのAnimatorController APIを使用して設定を変更します。

## 概要

- **Unityバージョン：** 2022.3.22f1
- **メニュー：** `nur_ieren > WDUnifier`
- **表示言語：** 日本語 / 英語

## 機能

- **複数の対象選択方法** — Animator Controllerを直接指定する方法と、GameObjectの階層を指定する方法に対応しています。
- **複数ファイルの一括選択** — Animator Controllerをツールにドラッグ＆ドロップして、複数のファイルを追加できます。
- **Animator Controllerの自動検出** — 指定したGameObject以下のAnimatorコンポーネントから、使用されているAnimator Controllerを検索します。
- **NDMFコンポーネントのスキャン** — Modular AvatarのMerge Animatorなどのツールで使用されるコンポーネントを含め、他のコンポーネント内に保存されたAnimator Controllerへの参照も検出します。
- **Animator Override Controllerへの対応** — Animator Override Controllerから、元となるAnimator Controllerを解決して処理します。
- **非アクティブオブジェクトへの対応** — 階層検索時に、非アクティブなGameObjectを対象に含めるか選択できます。
- **Write Defaultsの一括切り替え** — 収集したAnimator Controller内のすべてのStateを、ONまたはOFFに設定します。
- **プレビュー機能** — 対象となるAnimator Controller、Stateの総数、変更が必要なStateの数を表示します。
- **進捗表示・キャンセル** — 一括処理中に進捗バーを表示し、処理をキャンセルできます。
- **詳細ログ** — Animator Controllerごとに、変更されたStateの数を出力します。
- **Undoへの対応** — Stateの変更をUnityのUndoシステムに記録します。
- **日本語・英語の切り替え** — 日本語と英語の表示を切り替えられます。選択した言語はEditorPrefsに保存されます。

## 動作要件

- Unity 2022.3.22f1

## 使い方

1. `nur_ieren > WDUnifier` を開きます。
2. 対象の選択方法を指定します。
   - **File** — Animator Controllerをドラッグ＆ドロップするか、オブジェクトフィールドから直接追加します。
   - **GameObject** — GameObjectを指定し、その階層内にあるAnimator Controllerへの参照を検索します。
3. GameObject指定の場合、非アクティブなオブジェクトも検索対象に含めるか選択します。
4. 変更後のWrite Defaultsの値を選択します。
   - `Set ON (1 / true)`
   - `Set OFF (0 / false)`
5. `Preview (check targets and change count)` をクリックし、対象のAnimator Controllerと変更が必要なStateの数を確認します。
6. `Run Batch Switch` をクリックし、確認画面で実行を確定します。
7. ログを確認し、処理結果を確認します。

ウィンドウ上部のツールバーから、日本語と英語の表示を切り替えることができます。

## 注意事項

- 収集されたAnimator Controller内のすべてのStateが処理対象となります。ネストされたState Machine内のStateも含まれます。
- GameObject指定では、Animatorコンポーネントに加えて、他のコンポーネント内に保存されたシリアライズ済みオブジェクト参照もスキャンします。
- Animator Override Controllerは、元となるAnimator Controllerに解決してから処理されます。
- 同じAnimator Controllerへの参照が複数存在する場合でも、処理は1回のみ行われます。
- 現在のWrite Defaultsの値が選択した値と異なるStateのみ変更されます。
- 処理をキャンセルした場合、それ以降の処理は停止しますが、すでに処理されたAnimator Controllerの変更は維持されます。
- UnityのUndo履歴が保持されている限り、Undoシステムを使用して変更を取り消せます。
- 大規模な一括処理を実行する前に、プロジェクトのバックアップを取ることを推奨します。

## 作者

- nur_ieren
- vor_ges

## ライセンス

このプロジェクトはMIT Licenseのもとで公開されています。詳細は `LICENSE` ファイルを参照してください。

[MIT License](https://opensource.org/license/mit)
