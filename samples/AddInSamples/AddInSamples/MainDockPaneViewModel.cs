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

            // レイヤーコンボボックスにレイヤーを格納
            foreach (var featureLayer in mapView.Map.Layers.OfType<BasicFeatureLayer>())
            {
                FeatureLayers.Add(featureLayer);
            }
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
