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
class Preview {
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
  var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown }; foreach(var theme in new[]{"Touch","Workspace"}) app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source=new Uri("/TheCafePOS_WPF;component/Themes/"+theme+".xaml",UriKind.Relative) });
  var auth=AuthService.Instance;auth.CreateAccount("quan-ly","PreviewOnly!2026","Owner");auth.Login("quan-ly","PreviewOnly!2026");auth.CreateAccount("thu-ngan","PreviewOnly!2026","Cashier");
  var store=DataStoreService.Instance;store.OpenShift(500000);
  var coffee=store.Products.Single(p=>p.Name=="Americano Classic");var item=OrderConfigurationService.Create(coffee,"M",100,100,2,Array.Empty<(string,int)>(),"Ít đá",store.Products);
  store.Checkout(new[]{item},"Cash",200000,"preview-order");HoldOrderService.Instance.HoldOrder(new ObservableCollection<OrderItem>{item},110000,"Khách chờ bạn");
  var windows=new Window[]{new ManagementWindow(),new FinanceWindow(),new PackagingStatusModal(),new ReportWindow(),new HeldOrdersModal(),new BlindDropModal(store.ExpectedCash),new PinApprovalModal("Hoàn tiền đơn #01 · 55.000đ"),new PasswordWindow(),new ProductConfigurationWindow(coffee),new TheCafePOS_WPF.Core.NumberPad("500000",false),new ReprintStickerModal(new PrintedStickerInfo {ProductName="Americano Classic"}),new LoginWindow(),new VietQRModal(110000,1,"preview-order"),new OptionCustomModal(coffee)};
  foreach(var window in windows){
   double width=double.IsNaN(window.Width)?600:window.Width;double height=double.IsNaN(window.Height)?650:window.Height-32;
   string name=window.GetType().Name;Render(window,name,width,height);
   var tabs=Descendants<TabControl>((DependencyObject)window.Content).FirstOrDefault();
   if(tabs!=null){for(int i=1;i<tabs.Items.Count;i++){tabs.SelectedIndex=i;Render(window,name+"-tab"+i,width,height);}tabs.SelectedIndex=0;Render(window,name+"-compact",Math.Max(860,window.MinWidth),580);}
  }
  var management=(ManagementWindow)windows[0];var search=(TextBox)management.FindName("MenuSearch");search.Text="Americano";
  var products=(DataGrid)management.FindName("Products");if(products.Items.Count!=3)throw new Exception("Menu search failed");products.SelectedIndex=0;
  var category=(ComboBox)management.FindName("ProductCategory");if(category.SelectedItem==null)throw new Exception("Category selection lost");
  Render(management,"ManagementWindow-selected",1120,728);
  category.IsDropDownOpen=true;category.ApplyTemplate();if(category.Template.FindName("PART_Popup",category)==null)throw new Exception("ComboBox popup missing");category.IsDropDownOpen=false;
  if(Descendants<ScrollViewer>((DependencyObject)management.Content).Count()==0)throw new Exception("Scrolling missing");
    Console.WriteLine("PASS search, selection, dropdown template and scroll hosts");
  var main=new TheCafePOS_WPF.MainWindow {WindowState=WindowState.Normal,Left=-10000,Top=-10000,ShowInTaskbar=false};
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
  var scroller=(ScrollViewer)main.FindName("CategoryScroller");
  main.Width=960;Flush();((Button)main.FindName("NextCategories")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Flush();
  Check(scroller.HorizontalOffset>0,"Category next button scrolls");
  scroller.ScrollToRightEnd();Flush();Check(!((Button)main.FindName("NextCategories")).IsEnabled,"End of categories disables next");
  scroller.ScrollToLeftEnd();Flush();Check(!((Button)main.FindName("PreviousCategories")).IsEnabled,"Start of categories disables previous");
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
  var cashBox=(TextBox)main.FindName("TxtCashGiven");cashBox.Text="12345";
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
  main.Close();app.Shutdown();
 }
}
