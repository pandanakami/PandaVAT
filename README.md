<span style="color:red;">(暫定書き その内ちゃんと書く)</span>

# PandaVAT
PandaVATはアニメーションをVAT(VertexAnimationTexture)化するためのUnityパッケージです<br>
主にVRChatのワールド向け<br>
(アバターにも使えると思うけど開始時間設定とかで困るかも)<br>

# 導入方法
## VCCの場合
1. [ここ](https://pandanakami.github.io/vpm-package-list/install/)からVCCにリポジトリ登録

2. VCCで対象プロジェクトにPandaVATを追加

## その他の場合
[Releases](https://github.com/pandanakami/PandaVAT/releases)から最新取ってきていい感じにPackagesの下に入れてください。<br>

# 使い方

## 1. 使用中のシェーダーをVAT化する<br>
使用中のシェーダーを以下のサンプルを見ていい感じにVAT化してください。<br>
`Packages/com.panda-nakami.pandavat/Runtime/Sample/SampleVATShader/VatSampleShad.shader`<br>
<br>
基本、Vertexシェーダーの最初にVAT座標取得処理を呼ぶだけです。<br>
サーフェスシェーダーは非対応です。<br>
<br>
## 2. アニメーションのVAT化(設定)<br>
`[ぱんだスクリプト]-[PandaVAT]`を選択してダイアログ開いてください。<br>
ダイアログに対象のアニメーションがある`Animator`と`1で作ったシェーダー`を指定してください。<br>
Animatorが持つ`AnimationClip一覧`と`Renderer一覧`が表示されるのでVAT対象にしたいやつを選んでください。<br>
Rendererは`MeshRendere`と`SkinnedMeshRenderer`が対象です。<br>
残りの設定をいい感じにしてください。<br>
<br>
## 3. アニメーションのVAT化(生成)<br>
ダイアログのGenerateボタン押してください<br>
<br>
`Asset保存場所`に指定した場所にテクスチャとマテリアルとメッシュができ、`オブジェクト生成位置`に指定した場所にそれを持つMeshRendererのGameObjectができます。<br>
メッシュは選択したAnimatorのTransformのLocal情報を加味します。できたGameObjectをAnimatorのある階層に持っていきPosition/Rotation/Scale初期化したらAnimatorの形に一致します。<br>
<br>
## 4. マテリアル設定<br>
以下設定項目<br>
・`VATテクスチャ`：VATのテクスチャ。生成されたのを使う。触らない<br>
・`VAT FPS`：テクスチャのFPS設定。触らない。<br>
・`VAT制御方法[時間/割合]`：VATを時間で制御するか割合で制御するか<br>
### ■時間の場合<br>
・`VATをループするか否か`：時間経過でループするか否か<br>
・`VAT開始時間[秒]`：アニメーションを開始する時間。ループ設定しててもこの時間までは開始しない[^1]<br>
・`VATスピード`：アニメーションのスピード[^1]<br>
・`開始前時間[秒]`：アニメーション開始前に動かない時間を設定できる[^1]<br>
・`開始後時間[秒]`：アニメーション終了後に動かない時間を設定できる[^1]<br>
### ■割合の場合<br>
・`VAT割合`：0から1の間でアニメーションの位置を設定できる[^1]<br>
<br>
## 5. 実行時のアニメーション制御<br>
### ・VAT制御方法が[時間]の場合<br>
スクリプトで`MaterialPropertyBlock`作って、開始時間をセットして`MeshRenderer`に指定する流れ<br>
<br>
こんな感じに使う。<br>

``` csharp
materialPropertyBlock.SetFloat("_VatStartTimeSec", Time.timeSinceLevelLoad);
meshRenderer.SetPropertyBlock(materialPropertyBlock);
```
### ・VAT制御方法が[割合]の場合<br>
アニメーションで割合を操作する使い方になると思います。<br>
<br>
# 回転補間モードについて<br>
通常のVATはテクスチャに頂点位置を書き込んでいる仕組み上、急速な回転に弱いです。<br>
例えば30FPSで1フレームで90°回転するような立方体で、描画するタイミングが1.5/30秒の場合、1フレーム目と2フレーム目のちょうど中間になり、線形補完のせいで立方体がとても小さくなってしまいます。<br>
回転補間モードは、これを防ぐために、テクスチャに各頂点が影響するボーンのPosition/Rotation/Scaleを持たせ、シェーダー内でRotationの補間にslerpを使用するようにしたモードです。<br>
シェーダーを回転補間モードにするには以下サンプルを参考にしてください<br>
`Packages/com.panda-nakami.pandavat/Runtime/Sample/SampleVATShader/VatSampleRotationInterpolatioinModeShad.shader`<br>
`Properties`の中が変わるくらいです。<br>
シェーダー変更した後、再生成してください。<br>
<br>
以下、回転補間モードのメリット・デメリット・制約です。<br>
<br>
## ・メリット<br>
回転アニメーションの場合テクスチャのFPS数を減らしても通常モードに比べてそれなりに元アニメーションに近い動きをしそう(願望)<br>
<br>
## ・デメリット<br>
通常モードに比べてシェーダー内の処理負荷が高い<br>
<br>
## ・制約<br>
### (1) 1つのボーンに100%追従している頂点しかVAT化できない<br>
できるけど、テクスチャが無限にでかくなるのでしない。<br>
<br>
### (2) Transformが`せん断`しているボーンはできない<br>
せん断例：親TransformがScale(2,1,1)で自TransformがRotation(0,45,0)のようなやつ。<br>
位置、回転、スケーリングから変換行列を再現できないやつ。<br>
式で言うと以下のようなボーン。
```
transform.localToWorldMatrix !=  Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale)
```
[^1]: UNITY_DEFINE_INSTANCED_PROPの対象<br>
