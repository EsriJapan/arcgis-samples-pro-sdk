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
        private const string _dockPaneID = "AddInSamples_MainDockPane";

        protected MainDockPaneViewModel()
        {
            // 選択ボタンを押すとExecuteSelectionTool()が実行される
            _selectionTool = new RelayCommand(() => ExecuteSelectionTool(), () => true);

            // イベントの登録
            MapSelectionChangedEvent.Subscribe(OnMapSelectionChanged);
            ActiveMapViewChangedEvent.Subscribe(OnActiveMapViewChanged);
            LayersAddedEvent.Subscribe(OnLayerAdded);
            LayersRemovedEvent.Subscribe(OnLayerRemoved);
        }

        /// <summary>
        /// 初期化
        /// </summary>
        protected override Task InitializeAsync()
        {
            GetLayers();
            return base.InitializeAsync();
        }

        #region コマンド（マップとの対話的操作）
        /// <summary>
        /// フィーチャ選択ボタン（マップツールを使用）
        /// </summary>
        private RelayCommand _selectionTool;
        public ICommand SelectionTool => _selectionTool;
        internal static void ExecuteSelectionTool()
        {
            // 作成したマップ ツールのDAMLIDを指定
            var cmd = FrameworkApplication.GetPlugInWrapper("AddInSamples_IdentifyFeatures") as ICommand;
            if (cmd.CanExecute(null))
                // マップツール起動
                cmd.Execute(null);
        }
        #endregion

        #region バインド用のプロパティ（マップとの対話的操作）
        /// <summary>
        /// レイヤー コンボ ボックス
        /// </summary>
        private ObservableCollection<BasicFeatureLayer> _featureLayers = new ObservableCollection<BasicFeatureLayer>();
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
        private BasicFeatureLayer _selectedFeatureLayer;
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
        private DataTable _selectedFeatureDataTable;
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
        private int _tabPage;

        public int TabPage
        {
            get { return _tabPage; }
            set
            {
                SetProperty(ref _tabPage, value, () => TabPage);
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
            // レイヤーを取得
            GetLayers();

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
                    _selectedFeatureDataTable = new DataTable();
                    foreach (var col in listColumnNames)
                    {
                        _selectedFeatureDataTable.Columns.Add(new DataColumn(col.Key, typeof(string)) { Caption = col.Value });
                    }
                    // DataGridに選択したフィーチャの属性を格納
                    foreach (var row in listValues)
                    {
                        var newRow = _selectedFeatureDataTable.NewRow();
                        newRow.ItemArray = row.ToArray();
                        _selectedFeatureDataTable.Rows.Add(newRow);
                    }

                    NotifyPropertyChanged(() => SelectedFeatureDataTable);

                    if (_selectedFeatureDataTable.Rows.Count > 0)
                    {
                        // ズーム
                        ZoomToSelection();
                    }
                        
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
            {
                FeatureLayers.Clear();
                return;
            }
                
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
