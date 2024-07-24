<span style="color:red;">(暫定書き その内ちゃんと書く)</span>

# PandaVAT
PandaVATはアニメーションをVAT(VertexAnimationTexture)化するためのUnityパッケージです<br>
主にVRChatのワールド向け<br>
(アバターにも使えると思うけど開始時間設定とかで困るかも)<br>

# 注意点
本システムでは頂点の座標とかをRGBA Halfの画像で16bit精度で保持させます。座標が大きかったり小さすぎたりするモデルの場合は見た目が劣化すると思います。アニメーションで大きく/小さくする場合も同じ。<br>
どこまでいい感じにいけるかはそのうち調べます。

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
基本、`PandaVatXXXMode.cginc`をインクルードしてVertexシェーダーの最初にVAT座標取得処理を呼ぶだけです。<br>
サーフェスシェーダーは非対応です。<br>
ShaderGraphも同じく。<br>
<br>
## 2. アニメーションのVAT化(設定)<br>
`[Window]-[ぱんだスクリプト]-[PandaVAT]`を選択してダイアログ開いてください。<br>
ダイアログに対象のアニメーションがある`Animator`と`1で作ったシェーダー`を指定してください。<br>
Animatorが持つ`AnimationClip一覧`と`Renderer一覧`が表示されるのでVAT対象にしたいやつを選んでください。<br>
Rendererは`MeshRendere`と`SkinnedMeshRenderer`が対象です。<br>
Rendererは複数選ぶとメッシュ結合しますが、制約で`マテリアルスロットは1個に結合されます`。そのうち複数マテリアルスロット対応します。<br>
メッシュの頂点数が出力テクスチャの幅になるので、頂点数多い場合は注意してください。とりあえず8192制限かけています。<br>
どうしても8192超える場合やテクスチャサイズを小さくしたい場合は、後述の`回転補間モード`を試してください。<br>
<br>
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
・`ON/OFFアニメーション有効`：表示/非表示のアニメーションをVATで有効にする設定。Rendererのenable/disable、GameObjectのActive/非Activeを加味。<br>(`回転補間モード`だと、複数SkinnedMeshRendererが同じArmatureを共有する場合、個別のON/OFFは正常に機能しません。)<br>
・`VAT制御方法[時間/割合]`：VATを時間で制御するか割合で制御するか<br>
### ■VAT制御方法が[時間]の場合<br>
・`VATをループするか否か`：時間経過でループするか否か<br>
・`VAT開始時間[秒]`：アニメーションを開始する時間。ループ設定しててもこの時間までは開始しない[^1]<br>
・`VATスピード`：アニメーションのスピード[^1]<br>
・`開始前時間[秒]`：アニメーション開始前に動かない時間を設定できる[^1]<br>
・`開始後時間[秒]`：アニメーション終了後に動かない時間を設定できる[^1]<br>
### ■VAT制御方法が[割合]の場合<br>
・`VAT割合`：0から1の間でアニメーションの位置を設定できる[^1]<br>
<br>
## 5. 実行時のアニメーション制御<br>
### ・VAT制御方法が[時間]の場合<br>
スクリプトで`MaterialPropertyBlock`作って、開始時間をセットして`MeshRenderer`に指定する流れ<br>
<br>
こんな感じに使う。<br>

``` csharp
//ワールド入ってからの時間(秒)。これがシェーダーの_Time.yと同じ
materialPropertyBlock.SetFloat("_VatStartTimeSec", Time.timeSinceLevelLoad);
meshRenderer.SetPropertyBlock(materialPropertyBlock);
```
### ・VAT制御方法が[割合]の場合<br>
アニメーションで割合を操作する使い方になると思います。<br>
<br>
# 回転補間モードについて<br>
通常のVATはテクスチャに頂点位置を書き込んでいる仕組み上、急速な回転に弱いです。<br>
例えば30FPSで1フレームで90°回転するような立方体で、描画するタイミングが1.5/30秒の場合、1フレーム目と2フレーム目のちょうど中間になり、線形補間のせいで立方体がとても小さくなってしまいます。(補間しなければそれはそれで表示が汚い。)<br>
![image](https://raw.githubusercontent.com/pandanakami/PandaVAT/images/images/rotation_interporation.png)
<br>
回転補間モードは、これを防ぐために、テクスチャに各頂点が影響するボーンのPosition/Rotation/Scaleを持たせ、シェーダー内でRotationの補間にslerpを使用するようにしたモードです。<br>
ありていに言えばシェーダーでスキニングしているだけですが、Rotationを補間できるように変換行列でなくPosition/Rotation/Scaleを持たせています。<br>
<br>
シェーダーを回転補間モードにするには以下サンプルを参考にしてください<br>
`Packages/com.panda-nakami.pandavat/Runtime/Sample/SampleVATShader/VatSampleRotationInterpolatioinModeShad.shader`<br>

シェーダー変更した後、テクスチャの再生成必要です。<br>
<br>
以下、回転補間モードのメリット・デメリット・制約です。<br>
<br>
## ・メリット<br>
### (1)テクスチャサイズはほとんどの場合で圧倒的に小さくなる
テクスチャの幅が頂点数でなくボーン数になるため。

### (2)回転アニメーションは元のアニメーションに近い動きをする<br>
<br>

## ・デメリット<br>
### (1)通常モードに比べてシェーダー内の処理負荷が高い
Quaternionのslerpしたり、変換行列作ったりしてるので。<br>

## ・制約<br>
### (1) 1つのボーンに100%追従している頂点しかVAT化できない<br>
そのうちボーン4つまでweight加味して追従できるようにする予定です。<br>
<br>

### (2) BlendShapeは非対応
(1)と合わせてこれができたらアバターのVAT化も現実味帯びますね<br>

### (3) Transformが`せん断`しているボーンはできない<br>
`せん断`例：親TransformがScale(2,1,1)で自TransformがRotation(0,45,0)のようなやつ。<br>
元が立方体だったのに菱餅みたいになるやつ。<br>
位置、回転、スケーリングから変換行列を再現できないやつ。<br>
式で言うと以下のようなボーン。
```
transform.localToWorldMatrix !=  Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale)
```

ターゲットのボーン自体がScale(2,1,1)、Rotation(0,45,0)みたいになっているのは大丈夫です。

# トラブルシューティング
## ・生成結果がおかしい場合
Unity再起動して再生成すると直るかもしれません。<br>

[^1]: UNITY_DEFINE_INSTANCED_PROPの対象<br>
