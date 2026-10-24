using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;
partial class Preview {
 static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject {
  for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T t)yield return t;foreach(var d in Descendants<T>(child))yield return d;}
 }
 static void Render(Window window,string name,double width,double height) {
  var root=(FrameworkElement)window.Content;root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout(); System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle); root.UpdateLayout();
  var bitmap=new RenderTargetBitmap((int)width,(int)height,96,96,PixelFormats.Pbgra32);
  var background=new DrawingVisual();using(var drawing=background.RenderOpen())drawing.DrawRectangle(window.Background??Brushes.White,null,new Rect(0,0,width,height));bitmap.Render(background);bitmap.Render(root);
  var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var output=File.Create($"artifacts/dialog-preview/{name}.png");encoder.Save(output);
  Console.WriteLine("Rendered "+name);
 }
 [STAThread] static void Main(){
  Directory.CreateDirectory("artifacts/dialog-preview");
  Environment.SetEnvironmentVariable("THECAFEPOS_DATA_DIR",Path.GetFullPath("artifacts/dialog-preview/data-"+Guid.NewGuid().ToString("N")));
  // Same container as the app, built after the data directory is chosen.
  var services=TheCafePOS_WPF.App.BuildServices();
  var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown }; foreach(var theme in new[]{"Touch","Workspace"}) app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source=new Uri("/TheCafePOS_WPF;component/Themes/"+theme+".xaml",UriKind.Relative) });
  var auth=services.GetRequiredService<AuthService>();auth.CreateAccount("quan-ly","PreviewOnly!2026","Owner");auth.Login("quan-ly","PreviewOnly!2026");auth.CreateAccount("thu-ngan","PreviewOnly!2026","Cashier");
  var store=services.GetRequiredService<DataStoreService>();store.OpenShift(500000);
  var coffee=store.Products.Single(p=>p.Name=="Americano Classic");var item=OrderConfigurationService.Create(coffee,"M",100,100,2,Array.Empty<(string,int)>(),"Ít đá",store.Products);
  store.Checkout(new[]{item},"Cash",200000,"preview-order");services.GetRequiredService<HoldOrderService>().HoldOrder(new ObservableCollection<OrderItem>{item},110000,"Khách chờ bạn");
  var dialogs=services.GetRequiredService<TheCafePOS_WPF.Views.IDialogFactory>();
  var windows=new Window[]{dialogs.Create<ManagementWindow>(null),dialogs.Create<FinanceWindow>(null),dialogs.Create<PackagingStatusModal>(null),dialogs.Create<ReportWindow>(null),dialogs.Create<HeldOrdersModal>(null),dialogs.Create<BlindDropModal>(null,store.ExpectedCash),dialogs.Create<PinApprovalModal>(null,"Hoàn tiền đơn #01 · 55.000đ"),dialogs.Create<PasswordWindow>(null),dialogs.Create<ProductConfigurationWindow>(null,coffee),new TheCafePOS_WPF.Core.NumberPad("500000",false),dialogs.Create<ReprintStickerModal>(null,new PrintedStickerInfo {ProductName="Americano Classic"}),dialogs.Create<LoginWindow>(null),dialogs.Create<VietQRModal>(null,110000m,1,"preview-order"),dialogs.Create<OptionCustomModal>(null,coffee),dialogs.Create<VouchersWindow>(null),dialogs.Create<PurchasingWindow>(null),dialogs.Create<StockReportWindow>(null),dialogs.Create<ReceiptWindow>(null,new TheCafePOS_WPF.Models.Order{DailyOrderNumber=7,SubtotalAmount=110000,VoucherCode="GIAM10",VoucherAmount=10000,VatPercent=8,VatAmount=7407,TotalAmount=100000,PaymentMethod="Mixed",TransferAmount=30000,CashGiven=100000,ChangeReturned=30000,Items={new TheCafePOS_WPF.Models.OrderItem{ProductName="Americano Classic",Size="M",SugarChoice="Ít ngọt",Quantity=2,BasePrice=55000}}},false)};
  foreach(var window in windows){
   double width=double.IsNaN(window.Width)?600:window.Width;double height=double.IsNaN(window.Height)?650:window.Height-32;
   string name=window.GetType().Name;Render(window,name,width,height);
   var tabs=Descendants<TabControl>((DependencyObject)window.Content).FirstOrDefault();
   if(tabs!=null){for(int i=1;i<tabs.Items.Count;i++){tabs.SelectedIndex=i;Render(window,name+"-tab"+i,width,height);}tabs.SelectedIndex=0;Render(window,name+"-compact",Math.Max(860,window.MinWidth),580);}
  }
  var management=(ManagementWindow)windows[0];var search=(TextBox)management.FindName("MenuSearch");search.Text="Americano";
  var products=(DataGrid)management.FindName("Products");if(products.Items.Count!=3)throw new Exception("Menu search failed");products.SelectedIndex=0;
  if(products.SelectedItem is not ProductAdminRow{CategoryName:not "—"})throw new Exception("Category name missing");
  Render(management,"ManagementWindow-selected",1120,728);
  management.Left=-10000;management.Top=-10000;management.ShowInTaskbar=false;management.Show();
  var frame=new System.Windows.Threading.DispatcherFrame();var wait=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(600)};wait.Tick+=(_,_)=>{wait.Stop();frame.Continue=false;};wait.Start();System.Windows.Threading.Dispatcher.PushFrame(frame);
  Render(management,"ManagementWindow-shown",1120,728);
  // Every tab, including the tools embedded in Kho / Khuyến mãi / Khách hàng / Cài đặt, must load inside the window.
  var managementTabs=(TabControl)((DockPanel)management.Content).Children.OfType<TabControl>().Single();
  // Flush is defined later around the main window; pump the dispatcher directly here.
  Action Pump=()=>{var f=new System.Windows.Threading.DispatcherFrame();System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,new Action(()=>f.Continue=false));System.Windows.Threading.Dispatcher.PushFrame(f);};
  // Two levels: the main tabs and, while each is open, the sub-tabs of Kho, Khuyến mãi and Cài đặt.
  void CheckTab(TabItem tabItem,string name){
    var toolHost=tabItem.Content as ContentControl ?? (tabItem.Content as DockPanel)?.Children.OfType<ContentControl>().FirstOrDefault();
    if(toolHost!=null&&toolHost.Name.EndsWith("Host")&&toolHost.Content is not FrameworkElement{IsLoaded:true})throw new Exception("Embedded tool did not load: "+tabItem.Header);
    Render(management,"ManagementWindow-tab"+name,1120,728);}
  for(int i=0;i<managementTabs.Items.Count;i++){managementTabs.SelectedIndex=i;Pump();var top=(TabItem)managementTabs.Items[i];CheckTab(top,"-"+i);
    if(top.Content is TabControl nested)for(int j=0;j<nested.Items.Count;j++){nested.SelectedIndex=j;Pump();CheckTab((TabItem)nested.Items[j],"-"+i+"-"+j);}}
  // Combo promotions are edited as slots; render the editor with that type picked.
  managementTabs.SelectedIndex=3;Pump();((TabControl)((TabItem)managementTabs.Items[3]).Content).SelectedIndex=0;Pump();
  var promoHost=(ContentControl)management.FindName("PromotionsHost");
  var promoType=Descendants<ComboBox>((DependencyObject)promoHost.Content).First(c=>c.ItemsSource is string[] types&&types.Contains("Combo"));
  promoType.SelectedItem="Combo";Pump();Render(management,"ManagementWindow-promotion-combo",1120,728);
  managementTabs.SelectedIndex=0;Pump();
  Check(true,"Every management tab and embedded tool loads");
  management.Hide();
  var report=(Window)windows[3];report.Left=-10000;report.Top=-10000;report.ShowInTaskbar=false;report.Show();
  frame=new System.Windows.Threading.DispatcherFrame();wait.Start();System.Windows.Threading.Dispatcher.PushFrame(frame);
  Render(report,"ReportWindow-shown",report.Width,report.Height-32);report.Hide();
  var category=(ComboBox)management.FindName("MenuCategoryFilter");category.IsDropDownOpen=true;category.ApplyTemplate();if(category.Template.FindName("PART_Popup",category)==null)throw new Exception("ComboBox popup missing");category.IsDropDownOpen=false;
  if(Descendants<ScrollViewer>((DependencyObject)management.Content).Count()==0)throw new Exception("Scrolling missing");
    Console.WriteLine("PASS search, selection, dropdown template and scroll hosts");
  var main=services.GetRequiredService<TheCafePOS_WPF.MainWindow>();main.WindowState=WindowState.Normal;main.Left=-10000;main.Top=-10000;main.ShowInTaskbar=false;
  main.Show();main.Activate();
  void Flush(){System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);main.UpdateLayout();}
  void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
  var input=(TextBox)main.FindName("TxtSearch");var hint=(TextBlock)main.FindName("TxtSearchPlaceholder");var other=(Button)main.FindName("BtnHeldOrders");
  other.Focus();input.Clear();Flush();Check(hint.Visibility==Visibility.Visible,"Empty unfocused search shows hint");
  input.Focus();Flush();Check(input.IsKeyboardFocusWithin && hint.Visibility==Visibility.Collapsed,"Focused search hides hint before typing");
  var host=(ScrollViewer)input.Template.FindName("PART_ContentHost",input);var before=host.TranslatePoint(new Point(),input);
  input.Text="Trà";Flush();Check(hint.Visibility==Visibility.Collapsed,"Vietnamese input never overlaps hint");
  input.Text=" ";other.Focus();Flush();Check(hint.Visibility==Visibility.Collapsed,"Whitespace input does not paint over hint");
  input.Clear();other.Focus();Flush();Check(hint.Visibility==Visibility.Visible,"Cleared and blurred search restores hint");
  var after=host.TranslatePoint(new Point(),input);Check(before==after,"Input content does not shift on focus");
  // Categories form a vertical rail beside the menu: one entry per row, left-aligned, never wider than the rail.
  var chips=(ItemsControl)main.FindName("PnlCategories");main.Width=960;Flush();
  var chipBoxes=chips.Items.Cast<object>().Select(i=>(FrameworkElement)chips.ItemContainerGenerator.ContainerFromItem(i)).Select(c=>(Pos:c.TranslatePoint(new Point(),chips),Width:c.ActualWidth)).ToList();
  Check(chipBoxes.All(c=>Math.Abs(c.Pos.X)<1&&c.Width<=chips.ActualWidth+1),"Category rail entries line up in one column");
  Check(chipBoxes.Select(c=>Math.Round(c.Pos.Y)).Distinct().Count()==chipBoxes.Count,"Each category has its own row");
  foreach(var width in new[]{960,1240}){main.Width=width;main.Height=680;Flush();Render(main,"MainWindow-audit-"+width,width,648);}
  var money=new TheCafePOS_WPF.Core.NumberPad("100000",false,"Tiền khách đưa","đ",55000);
  Render(money,"NumberPad-cash",580,588);
  Check(money.AmountPreview.Text=="100.000 đ" && money.BalancePreview.Text=="Tiền thối: 45.000 đ","Cash keypad formats money and calculates change");
  money.SetAmount(50000);Check(money.BalancePreview.Text=="Còn thiếu: 5.000 đ","Cash keypad shows short payment");
  money.Input.SelectAll();money.Insert("200000");Check(money.Input.Text=="200000","Touch digits replace selected amount");
  money.Input.CaretIndex=3;money.Backspace();Check(money.Input.Text=="20000","Backspace edits at caret");
  money.Input.SelectAll();money.Backspace();Check(money.Input.Text=="" && !money.ApplyButton.IsEnabled,"Empty input cannot be applied");
  foreach(var bad in new[]{"12.5","-1","abc","1000000000000","1,000"}){money.Input.Text=bad;Check(!money.ApplyButton.IsEnabled,"Reject keypad value "+bad);}
  money.SetAmount(0);Check(money.ApplyButton.IsEnabled,"Zero remains valid as an entered amount without authorizing payment");
  var stockPad=new TheCafePOS_WPF.Core.NumberPad("-12",true,"Điều chỉnh kho","cái",-1,2147483647);
  Check(stockPad.ApplyButton.IsEnabled && stockPad.AmountPreview.Text=="-12 cái","Signed quantities use their own unit");
  stockPad.Input.Text="2147483648";Check(!stockPad.ApplyButton.IsEnabled,"Stock keypad enforces Int32 range");
  var refundPad=new TheCafePOS_WPF.Core.NumberPad("55001",false,"Số tiền hoàn","đ",-1,55000);
  Check(!refundPad.ApplyButton.IsEnabled,"Refund keypad enforces remaining refundable amount");
  // Keypad fixture: a numeric field in its own offscreen window, styled by the real application resources.
  var cashBox=new TextBox{Text="12345"};TheCafePOS_WPF.Core.TouchInput.SetNumeric(cashBox,true);TheCafePOS_WPF.Core.TouchInput.SetLabel(cashBox,"Tiền khách đưa");
  var cashHost=new Window{Owner=main,Content=cashBox,Width=320,Height=120,Left=-10000,Top=-10000,ShowInTaskbar=false,ShowActivated=false};cashHost.Show();
  cashBox.ApplyTemplate();var opener=(Button)cashBox.Template.FindName("NumberPadButton",cashBox);
  Check(opener.Visibility==Visibility.Visible && ((System.Windows.Input.RoutedCommand)opener.Command).CanExecute(null,opener.CommandTarget),"Numeric fields expose explicit keypad command");
  int orderCount=store.CompletedOrders.Count;
  System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(()=>{
    var opened=Application.Current.Windows.OfType<TheCafePOS_WPF.Core.NumberPad>().Last(w=>w.IsVisible);
    opened.SetAmount(200000);opened.Close();
  }));
  TheCafePOS_WPF.Core.TouchInput.OpenCommand.Execute(null,cashBox);
  Check(cashBox.Text=="12345","Cancel keypad preserves original field");
  System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(()=>{
    var opened=Application.Current.Windows.OfType<TheCafePOS_WPF.Core.NumberPad>().Last(w=>w.IsVisible);
    opened.SetAmount(200000);opened.ApplyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  }));
  TheCafePOS_WPF.Core.TouchInput.OpenCommand.Execute(null,cashBox);
  Check(cashBox.Text=="200000" && store.CompletedOrders.Count==orderCount,"Apply updates cash field without creating payment");
  cashHost.Close();
  TheCafePOS_WPF.Core.Toast.Show(main,"Đã thêm 2 × Combo Sắc Xinh - Matcha Latte Tây Bắc (M)",false,("Tùy chỉnh",()=>{}));Flush();
  var addToast=Application.Current.Windows.OfType<Window>().Last(w=>w.IsVisible&&w.WindowStyle==WindowStyle.None&&w.Content is Border);Render(addToast,"Toast-added",Math.Max(320,addToast.ActualWidth),Math.Max(60,addToast.ActualHeight));addToast.Close();
  TheCafePOS_WPF.Core.Toast.Show(main,"Đã xóa Americano Classic khỏi đơn",false,("Hoàn tác",()=>{}));Flush();
  var toastWindow=Application.Current.Windows.OfType<Window>().Last(w=>w.IsVisible&&w.WindowStyle==WindowStyle.None&&w.Content is Border);
  Render(toastWindow,"Toast-undo",Math.Max(320,toastWindow.ActualWidth),Math.Max(60,toastWindow.ActualHeight));toastWindow.Close();
  CheckCartFlows(main, store);
  main.Close();app.Shutdown();
 }
}
