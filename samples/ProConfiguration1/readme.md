# 構成管理画面のカスタマイズ

## 概要

このサンプルは、ArcGIS Pro を起動したときに表示される次の 2 つの画面について実装しています。
* スプラッシュ スクリーン画面：タイトル文字列と画像の変更
* プロジェクト選択画面：既存のプロジェクト数をカウントして画面に表示する

## 動作確認環境

* ArcGIS Pro 2.4

## 実装解説

次のブログで詳しく解説しています。

* [Pro SDK を使用した ArcGIS Pro の拡張⑤：管理構成画面のカスタマイズ](https://community.esri.com/docs/DOC-12651)

## 設定

ArcGIS Pro 起動パスと、参照パッケージの設定を行う必要があります。

<u>ArcGIS Pro 起動パスと参照パッケージの設定</u>
プロジェクトを開いて [参照] から右クリックして [ArcGIS Pro の参照先を修正] を選択します。</br>
「プロジェクト設定が変更されます。続行しますか?」のダイアログが表示されたら [はい] を選択します。[参照] を展開して、特に警告エラーが表示されない場合はプロジェクトを起動することができます。</br>
※[プロジェクト<プロジェクト名>は環境外で変更されています]のダイアログが表示される場合は [再読み込み] を選択します。

ArcGIS Pro の exe ファイルの指定も指定先が合っているか確認します。プロジェクトのプロパティを展開して、[デバッグ] 内を表示すると、ArcGIS Pro no
実行ファイル参照先が定義されています。

## 関連ブログ

Pro SDK を使用した ArcGIS Pro の拡張 シリーズ ブログ
* [Pro SDK を使用した ArcGIS Pro の拡張①：ArcGIS Pro SDK とは?](https://community.esri.com/docs/DOC-11507)
* [Pro SDK を使用した ArcGIS Pro の拡張②：環境構築](https://community.esri.com/docs/DOC-11648)
* [Pro SDK を使用した ArcGIS Pro の拡張③：アドインプロジェクトの構成](https://community.esri.com/docs/DOC-11974)
* [Pro SDK を使用した ArcGIS Pro の拡張④：アドインの開発](https://community.esri.com/docs/DOC-12492)
* [Pro SDK を使用した ArcGIS Pro の拡張⑤：管理構成画面のカスタマイズ](https://community.esri.com/docs/DOC-12651)

### その他

このプロジェクトおよびブログで使用している画像は [NASA](https://www.nasa.gov/) がオープンデータとして公開しているものを使用しました。