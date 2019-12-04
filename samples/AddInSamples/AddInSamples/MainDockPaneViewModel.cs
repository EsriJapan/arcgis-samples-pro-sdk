using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Events;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;

namespace AddInSamples
{
    internal class MainDockPaneViewModel : DockPane
    {
        #region Private Properties
        private const string _dockPaneID = "AddInSamples_MainDockPane";

        // マップツール
        private RelayCommand _selectionTool;

        // イベント登録/解除用トークン
        private SubscriptionToken _mapSelectionChangedEvent = null;
        
        // レイヤー コンボ ボックス
        private ObservableCollection<BasicFeatureLayer> _featureLayers = new ObservableCollection<BasicFeatureLayer>();
        private BasicFeatureLayer _selectedFeatureLayer;

        // DataGrid
        private DataTable _selectedFeatureDataTable;
        private ICommand _dataGridDoubleClick;

        // タブ
        private int _tabPage;


        // レンダリング：レイヤー コンボボックス
        private ObservableCollection<FeatureLayer> _renderingLayers = new ObservableCollection<FeatureLayer>();
        private FeatureLayer _selectedRenderingLayer;
        // レンダリング：フィールド コンボボックス
        private List<String> _fields = new List<String>();
        private string _selectedField;
        // レンダリング：レンダリング手法 コンボボックス
        private ObservableCollection<string> _renderingMethods = new ObservableCollection<string>
        {
            "個別値レンダリング",
            "等級色レンダリング"
        };
        private string _selectedRenderingMethod;
        private ICommand _executeRendering;
        #endregion Private Properties

        #region 起動時
        /// <summary>
        /// コンストラクタ
        /// </summary>
        protected MainDockPaneViewModel()
        {
            // 選択ボタンを押すとExecuteSelectionTool()が実行される
            _selectionTool = new RelayCommand(() => ExecuteSelectionTool(), () => true);

            // DataDridをダブルクリックするとExecuteDataGridDoubleClick()が実行される
            _dataGridDoubleClick = new RelayCommand(() => ExecuteDataGridDoubleClick(), () => true);

            // レンダリング タブの実行ボタンを押すと ExecuteRenderingClick() が実行される
            _executeRendering = new RelayCommand(() => ExecuteRenderingClick(), () => true);

            // イベントの登録
            ActiveMapViewChangedEvent.Subscribe(OnActiveMapViewChanged);
            LayersAddedEvent.Subscribe(OnLayerAdded);
            LayersRemovedEvent.Subscribe(OnLayerRemoved);
        }

        /// <summary>
        /// ドッキングウインドウの表示/非表示でイベントを登録/解除
        /// </summary>
        protected override void OnShow(bool isVisible)
        {
            if (isVisible && _mapSelectionChangedEvent == null)
            {
                // イベントの登録
                _mapSelectionChangedEvent = MapSelectionChangedEvent.Subscribe(OnMapSelectionChanged);
            }

            if (!isVisible && _mapSelectionChangedEvent != null)
            {
                // イベントの解除
                MapSelectionChangedEvent.Unsubscribe(_mapSelectionChangedEvent);
                _mapSelectionChangedEvent = null;

            }
        }
        
        /// <summary>
        /// 初期化
        /// </summary>
        protected override Task InitializeAsync()
        {
            GetLayers();
            return base.InitializeAsync();
        }
        #endregion

        #region コマンド（マップとの対話的操作）
        /// <summary>
        /// フィーチャ選択ボタン（マップツールを使用）
        /// </summary>
        public ICommand SelectionTool => _selectionTool;
        internal static void ExecuteSelectionTool()
        {
            // 作成したマップ ツールのDAMLIDを指定
            var cmd = FrameworkApplication.GetPlugInWrapper("AddInSamples_IdentifyFeatures") as ICommand;
            if (cmd.CanExecute(null))
                // マップツール起動
                cmd.Execute(null);
        }

        /// <summary>
        /// DataGridをダブルクリック時にフィーチャにズーム
        /// </summary>
        public ICommand DataGridDoubleClick => _dataGridDoubleClick;
        private void ExecuteDataGridDoubleClick()
        {
            QueuedTask.Run(() =>
            {
                var oid = _selectedFeature.Row["ObjectId"];
                // 選択フィーチャにズーム
                MapView.Active.ZoomTo(_selectedFeatureLayer, Convert.ToInt64(oid), TimeSpan.Zero, false);

            });
        }
        #endregion

        #region コマンド（レンダリング）
        /// <summary>
        /// 実行ボタンをクリック
        /// </summary>
        public ICommand ExecuteRendering => _executeRendering;
        private void ExecuteRenderingClick()
        {

            // コンボ ボックスでレイヤー、フィールド、レンダリング手法が選択されているかをチェックする
            if (_selectedRenderingLayer is null)
            {
                MessageBox.Show("レイヤーを選択してください。");
            }
            else if (_selectedField is null)
            {
                MessageBox.Show("フィールドを選択してください。");
            }
            else if (_selectedRenderingMethod is null)
            {
                MessageBox.Show("レンダリング手法を選択してください。");
            }
            else
            {
                try
                {
                    // レンダラー作成の処理を実装
                    QueuedTask.Run(() =>
                    {

                        // レイヤー名で検索してマップ上のレイヤーを取得する
                        var lyr = MapView.Active.Map.FindLayers(_selectedRenderingLayer.Name).FirstOrDefault() as FeatureLayer;

                        // 「ArcGIS カラー」（ArcGIS Pro の表示言語が英語の場合は、「ArcGIS Colors」を指定）プロジェクト スタイル アイテムを取得
                        StyleProjectItem style = Project.Current.GetItems<StyleProjectItem>().FirstOrDefault(s => s.Name == "ArcGIS カラー");

                        // 名前で検索してカラーランプ アイテムを取得する（ArcGIS Pro の表示言語が英語の場合は、「Spectrum - Full Light」を指定）
                        IList<ColorRampStyleItem> colorRampList = style.SearchColorRamps("フル スペクトル (明るい)");

                        if (_selectedRenderingMethod == "個別値レンダリング")
                        {

                            // 個別値レンダラーの定義を作成する
                            UniqueValueRendererDefinition uvrDef = new
                                UniqueValueRendererDefinition()
                            {
                                ValueFields = new String[] { _selectedField },　// 分類に使用するフィールド
                                ColorRamp = colorRampList[0].ColorRamp, // カラーランプ
                            };

                            // 個別値レンダラーを作成する
                            CIMUniqueValueRenderer cimRenderer = (CIMUniqueValueRenderer)lyr.CreateRenderer(uvrDef);
                            // レンダラーをレイヤーに設定する
                            lyr.SetRenderer(cimRenderer);

                        }
                        else // 等級色レンダリングの場合
                        {
                            if (GetNumericField(lyr, _selectedField)) // 数値型のフィールドを選択している場合のみ実行する
                            {
                                // 等級色レンダラーの定義を作成する
                                GraduatedColorsRendererDefinition gcDef = new GraduatedColorsRendererDefinition()
                                {
                                    ClassificationField = _selectedField,　// 分類に使用するフィールド
                                    ColorRamp = colorRampList[0].ColorRamp, // カラーランプ
                                    ClassificationMethod = ClassificationMethod.NaturalBreaks, // 分類方法（自然分類）
                                    BreakCount = 5, // 分類数（5段階で分類）
                                };

                                // 等級色（クラス分類）レンダラーを作成する
                                CIMClassBreaksRenderer cimClassBreakRenderer = (CIMClassBreaksRenderer)lyr.CreateRenderer(gcDef);
                                // レンダラーをレイヤーに設定する
                                lyr.SetRenderer(cimClassBreakRenderer);
                            }
                        }

                    });
                }
                catch (Exception)
                {
                    MessageBox.Show("レンダリングに失敗しました。");
                }

            }

        }
        #endregion


        #region バインド用のプロパティ（マップとの対話的操作）
        /// <summary>
        /// レイヤー コンボ ボックス
        /// </summary>
        public ObservableCollection<BasicFeatureLayer> FeatureLayers
        {
            get { return _featureLayers; }
            set
            {
                SetProperty(ref _featureLayers, value, () => FeatureLayers);
            }
        }

        /// <summary>
        /// レイヤー コンボ ボックスで選択しているレイヤー
        /// </summary>
        public BasicFeatureLayer SelectedFeatureLayer
        {
            get { return _selectedFeatureLayer; }
            set
            {
                SetProperty(ref _selectedFeatureLayer, value, () => SelectedFeatureLayer);

                OnMapSelectionChanged(null);
            }
        }

        /// <summary>
        /// DataGrid
        /// </summary>
        public DataTable SelectedFeatureDataTable
        {
            get { return _selectedFeatureDataTable; }
            set
            {
                SetProperty(ref _selectedFeatureDataTable, value, () => SelectedFeatureDataTable);
            }
        }

        /// <summary>
        /// タブ
        /// </summary>
        public int TabPage
        {
            get { return _tabPage; }
            set
            {
                SetProperty(ref _tabPage, value, () => TabPage);
            }
        }

        /// <summary>
        /// フィーチャの選択
        /// <summary>
        private DataRowView _selectedFeature = null;
        public DataRowView SelectedFeature
        {
            get
            {
                return _selectedFeature;
            }
            set
            {
                SetProperty(ref _selectedFeature, value, () => SelectedFeature);

                if (_selectedFeature == null || SelectedFeatureLayer == null)
                    return;
                // フィーチャの強調
                FlashFeatures(Convert.ToInt64(_selectedFeature.Row["ObjectId"]));
            }
        }


        #endregion


        #region バインド用のプロパティ（レンダラリング）
        /// <summary>
        /// レイヤー コンボ ボックス
        /// </summary>
        public ObservableCollection<FeatureLayer> RenderingLayers
        {
            get { return _renderingLayers; }
            set
            {
                SetProperty(ref _renderingLayers, value, () => RenderingLayers);
            }
        }

        /// <summary>
        /// レイヤー コンボ ボックスで選択しているレイヤー
        /// </summary>
        public FeatureLayer SelectedRenderingLayer
        {
            get { return _selectedRenderingLayer; }
            set
            {
                SetProperty(ref _selectedRenderingLayer, value, () => SelectedRenderingLayer);

                if (_selectedRenderingLayer == null)
                {
                    Fields = null;
                    SelectedRenderingMethod = null;
                    return;
                }

                GetFields();
            }
        }

        /// <summary>
        /// フィールド コンボ ボックス
        /// </summary>
        public List<String> Fields
        {
            get { return _fields; }
            set
            {
                SetProperty(ref _fields, value, () => Fields);
            }
        }

        /// <summary>
        /// フィールド コンボ ボックスで選択しているフィールド
        /// </summary>
        public string SelectedField
        {
            get { return _selectedField; }
            set
            {
                SetProperty(ref _selectedField, value, () => SelectedField);
            }
        }

        /// <summary>
        /// レンダリング手法コンボ ボックス
        /// </summary>
        public ObservableCollection<string> RenderingMethods
        {
            get
            {
                return _renderingMethods;
            }
            set { SetProperty(ref _renderingMethods, value, () => RenderingMethods); }
        }

        /// <summary>
        /// レンダリング手法コンボ ボックスで選択しているレンダリング手法
        /// </summary>
        public string SelectedRenderingMethod
        {
            get { return _selectedRenderingMethod; }
            set
            {
                SetProperty(ref _selectedRenderingMethod, value, () => SelectedRenderingMethod);
            }
        }
        #endregion


        #region イベントハンドラー
        /// <summary>
        /// レイヤーがマップに追加された場合に発生
        /// </summary>
        private void OnLayerAdded(LayerEventsArgs args)
        {
            // 追加したレイヤーをコンボボックスに追加
            foreach (var featureLayer in args.Layers.OfType<BasicFeatureLayer>().ToList())
            {
                FeatureLayers.Add(featureLayer);
            }
        }

        /// <summary>
        /// レイヤーがマップから削除された場合に発生
        /// </summary>
        private void OnLayerRemoved(LayerEventsArgs args)
        {
            // 削除したレイヤーをコンボボックスから削除
            foreach (var featureLayer in args.Layers.OfType<BasicFeatureLayer>().ToList())
            {
                if (_featureLayers.Contains(featureLayer))
                {
                    FeatureLayers.Remove(featureLayer);
                }
            }

            // DataGridをクリアして選択しているフィーチャを解除
            Clear();
        }

        /// <summary>
        /// アクティブなマップビューが変わった場合に発生
        /// </summary>
        private void OnActiveMapViewChanged(ActiveMapViewChangedEventArgs args)
        {
            // レイヤーを取得
            GetLayers();

            // DataGridをクリアして選択しているフィーチャを解除
            Clear();
        }

        /// <summary>
        /// マップ内の選択状態が変わった場合に発生
        /// </summary>
        private void OnMapSelectionChanged(MapSelectionChangedEventArgs args)
        {
            var mapView = MapView.Active;
            if (mapView == null)
                return;

            QueuedTask.Run(() =>
            {
                // レイヤーを選択していない場合
                if (_selectedFeatureLayer == null)
                    return;

                try
                {
                    var listColumnNames = new List<KeyValuePair<string, string>>();
                    var listValues = new List<List<string>>();

                    // 選択したフィーチャを処理する
                    using (var rowCursor = _selectedFeatureLayer.GetSelection().Search(null))
                    {
                        bool bDefineColumns = true;
                        while (rowCursor.MoveNext())
                        {
                            var anyRow = rowCursor.Current;
                            if (bDefineColumns)
                            {
                                // 選択したフィーチャのフィールドを取得
                                foreach (var fld in anyRow.GetFields().Where(fld => fld.FieldType != FieldType.Geometry))
                                {
                                    listColumnNames.Add(new KeyValuePair<string, string>(fld.Name, fld.AliasName));
                                }
                            }
                            // 選択したフィーチャの属性を取得
                            var newRow = new List<string>();
                            foreach (var fld in anyRow.GetFields().Where(fld => fld.FieldType != FieldType.Geometry))
                            {
                                newRow.Add((anyRow[fld.Name] == null) ? string.Empty : anyRow[fld.Name].ToString());
                            }
                            listValues.Add(newRow);
                            bDefineColumns = false;
                        }

                    }

                    // DataGridにカラムを設定
                    SelectedFeatureDataTable = new DataTable();
                    foreach (var col in listColumnNames)
                    {
                        SelectedFeatureDataTable.Columns.Add(new DataColumn(col.Key, typeof(string)) { Caption = col.Value });
                    }

                    // DataGridに選択したフィーチャの属性を格納
                    foreach (var row in listValues)
                    {
                        var newRow = SelectedFeatureDataTable.NewRow();
                        newRow.ItemArray = row.ToArray();
                        SelectedFeatureDataTable.Rows.Add(newRow);
                    }

                    if (_selectedFeatureDataTable.Rows.Count > 0)
                    {
                        // ズーム
                        ZoomToSelection();
                    }

                    // データが多い場合たまにDataGridにデータが表示されないことがある。これで回避。
                    NotifyPropertyChanged(() => SelectedFeatureDataTable);

                }
                catch (Exception)
                {
                    MessageBox.Show("フィーチャ属性の抽出に失敗しました。");
                }
            });
        }
        #endregion


        #region 共通処理
        /// <summary>
        /// レイヤーコンボボックスにレイヤーを格納します
        /// </summary>
        private void GetLayers()
        {
            // アクティブなマップビューを取得
            var mapView = MapView.Active;
            if (mapView == null)
                return;

            // コンボボックスに格納されているレイヤーをクリア
            FeatureLayers.Clear();
            RenderingLayers.Clear();

            // レイヤーコンボボックスにレイヤーを格納
            foreach (var featureLayer in mapView.Map.Layers.OfType<BasicFeatureLayer>())
            {
                FeatureLayers.Add(featureLayer);
            }

            // レンダリング タブのレイヤー コンボボックスにレイヤーを格納
            var renderingLayers = MapView.Active.Map.Layers.OfType<BasicFeatureLayer>().Where(f => f.GetType().Name != "AnnotationLayer");
            RenderingLayers.Clear();
            foreach (var renderingLayer in renderingLayers.Where(f => f.GetType().Name != "AnnotationLayer")) RenderingLayers.Add(renderingLayer as FeatureLayer);

        }

        /// <summary>
        /// DataGridと選択しているフィーチャのクリア処理
        /// </summary>
        private void Clear()
        {
            // DataGrid をクリア
            SelectedFeatureDataTable = null;

            // 選択しているフィーチャを解除
            QueuedTask.Run(() =>
            {
                var mapView = MapView.Active;
                if (mapView != null)
                {
                    MapView.Active.Map.SetSelection(null);
                }
            });
        }

        /// <summary>
        /// 選択しているフィーチャにズーム
        /// </summary>
        private void ZoomToSelection()
        {
            var mapView = MapView.Active;
            if (mapView == null)
                return;

            QueuedTask.Run(() =>
            {
                // スケッチしたジオメトリと交差したフィーチャを選択
                var selection = mapView.Map.GetSelection()
                      .Where(kvp => kvp.Key is BasicFeatureLayer)
                      .Select(kvp => (BasicFeatureLayer)kvp.Key);

                // 選択したフィーチャにズーム
                mapView.ZoomTo(selection, true);
            });
        }
        #endregion

        #region グラフィックの作成
        /// <summary>
        /// グラフィックの作成
        /// </summary>
        private void CreateGraphic(FeatureClass featureClass, QueryFilter queryFilter)
        {
            var mapView = MapView.Active;

            using (RowCursor rowCursor = featureClass.Search(queryFilter, true))
            {
                rowCursor.MoveNext();

                //レコードを取得
                using (Row row = rowCursor.Current)
                {
                    Feature feature = row as Feature;
                    Geometry shape = feature.GetShape();

                    RemoveFromMapOverlay(); // 既存のグラフィックを削除

                    switch (shape.GeometryType)
                    {
                        // ポイントの場合(マルチには対応していません)
                        case GeometryType.Point:
                            // ポイント作成
                            var point = shape as MapPoint;
                            MapPoint mapPoint = MapPointBuilder.CreateMapPoint(point.X, point.Y, shape.SpatialReference);

                            // グラフィック作成
                            var pointGraphic = new CIMPointGraphic();
                            pointGraphic.Location = mapPoint;

                            // シンボル作成
                            CIMPointSymbol pointSymbol = SymbolFactory.Instance.ConstructPointSymbol(ColorFactory.Instance.RedRGB, 5);
                            pointGraphic.Symbol = pointSymbol.MakeSymbolReference();

                            // グラフィックをマップビューに追加
                            _overlayObject = mapView.AddOverlay(pointGraphic);

                            break;

                        case GeometryType.Polygon:

                            // アノテーションの場合
                            if (feature.GetType().Name == "AnnotationFeature")
                            {
                                // グラフィック作成
                                var annoGraphic = new CIMPolygonGraphic();
                                annoGraphic.Polygon = shape as Polygon;

                                // シンボル作成
                                CIMStroke outline = SymbolFactory.Instance.ConstructStroke(ColorFactory.Instance.RedRGB, 2, SimpleLineStyle.Solid);
                                CIMPolygonSymbol polygonSymbol = SymbolFactory.Instance.ConstructPolygonSymbol(ColorFactory.Instance.BlueRGB, SimpleFillStyle.Null, outline);
                                annoGraphic.Symbol = polygonSymbol.MakeSymbolReference();

                                // グラフィックをマップビューに追加
                                _overlayObject = mapView.AddOverlay(annoGraphic);
                            }
                            else
                            {
                                // グラフィック作成
                                var polygonGraphic = new CIMPolygonGraphic();
                                polygonGraphic.Polygon = shape as Polygon;

                                // シンボル作成
                                CIMPolygonSymbol polygonSymbol = SymbolFactory.Instance.ConstructPolygonSymbol(ColorFactory.Instance.RedRGB);
                                polygonGraphic.Symbol = polygonSymbol.MakeSymbolReference();

                                // グラフィックをマップビューに追加
                                _overlayObject = mapView.AddOverlay(polygonGraphic);
                            }

                            break;

                        case GeometryType.Polyline:

                            // グラフィック作成
                            var lineGraphic = new CIMLineGraphic();
                            lineGraphic.Line = shape as Polyline;

                            // シンボル作成
                            CIMLineSymbol lineSymbol = SymbolFactory.Instance.ConstructLineSymbol(ColorFactory.Instance.RedRGB, 5);
                            lineGraphic.Symbol = lineSymbol.MakeSymbolReference();

                            // グラフィックをマップビューに追加
                            _overlayObject = mapView.AddOverlay(lineGraphic);

                            break;

                        default:
                            break;
                    }
                }

            }
        }

        /// <summary>
        /// フィーチャの強調
        /// </summary>
        private void FlashFeatures(long oid)
        {
            var mapView = MapView.Active;
            if (mapView == null)
                return;

            QueryFilter queryFilter = new QueryFilter
            {
                WhereClause = "ObjectId =" + oid,
            };

            try
            {
                QueuedTask.Run(() =>
                {

                    var annotationLayer = _selectedFeatureLayer as AnnotationLayer;
                    // アノテーションの場合
                    if (annotationLayer != null)
                    {
                        CreateGraphic(annotationLayer.GetFeatureClass(), queryFilter);
                    }
                    // アノテーションでない場合
                    else
                    {
                        var featureLayer = _selectedFeatureLayer as FeatureLayer;
                        CreateGraphic(featureLayer.GetFeatureClass(), queryFilter);
                    }
                });
            }
            catch
            {
                MessageBox.Show("フィーチャの強調に失敗しました。");
            }

        }

        /// <summary>
        /// 既存のグラフィックを削除
        /// </summary>
        private static System.IDisposable _overlayObject = null;
        private void RemoveFromMapOverlay()
        {
            if (_overlayObject != null)
            {
                _overlayObject.Dispose();
                _overlayObject = null;
            }
        }
        #endregion

        #region フィールドの取得（レンダリング）
        /// <summary>
        /// コンボボックスで選択したレイヤーのフィールド名のリストを取得
        /// </summary>
        private void GetFields()
        {
            // アクティブ（選択状態）なマップを取得する
            var mapView = MapView.Active;

            if (mapView == null || _selectedRenderingLayer == null)
                return;

            // レイヤーの属性テーブルにアクセスして、フィールド名を Fields 配列に格納する　
            QueuedTask.Run(() =>
            {
                // レイヤー名で検索してマップ上のレイヤーを取得する
                var featureLayer = MapView.Active.Map.FindLayers(_selectedRenderingLayer.Name).FirstOrDefault() as FeatureLayer;
                var flf = featureLayer.GetTable().GetDefinition().GetFields();

                // 文字列型または数値型のフィールドのフィールド名を抽出する
                Fields = flf.Where(f => f.FieldType == FieldType.Integer | f.FieldType == FieldType.SmallInteger | f.FieldType == FieldType.String | f.FieldType == FieldType.Double).Select(f => f.Name).ToList();

            });
        }
        #endregion

        #region 数値フィールドの抽出（レンダリング）
        /// <summary>
        /// 等級色レンダリングの場合に数値フィールドのみを返す
        /// </summary>
        internal static bool IsNumericFieldType(FieldType type)
        {
            switch (type)
            {
                // 以下のフィールドタイプのみを許容する
                case FieldType.Double:
                case FieldType.Integer:
                case FieldType.Single:
                case FieldType.SmallInteger:
                    return true;
                default:
                    return false;
            }
        }

        internal static bool GetNumericField(FeatureLayer featureLayer, string field)
        {
            // 数値型のフィールドか確認する
            IEnumerable<FieldDescription> numericField = featureLayer.GetFieldDescriptions().Where(f => IsNumericFieldType(f.Type) && f.Name == field);

            if (numericField.Any())
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        #endregion


        /// <summary>
        /// Show the DockPane.
        /// </summary>
        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane == null)
                return;

            pane.Activate();
        }

        /// <summary>
        /// Text shown near the top of the DockPane.
        /// </summary>
        private string _heading = "";
        public string Heading
        {
            get { return _heading; }
            set
            {
                SetProperty(ref _heading, value, () => Heading);
            }
        }
    }

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class MainDockPane_ShowButton : Button
    {
        protected override void OnClick()
        {
            MainDockPaneViewModel.Show();
        }
    }
}
