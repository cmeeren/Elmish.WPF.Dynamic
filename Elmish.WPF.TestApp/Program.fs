module Elmish.Wpf.TestApp

open System
open System.Windows
open System.Windows.Controls
open System.Windows.Media
open Elmish
open Elmish.WPF.Dynamic

let rnd = Random ()


let shuffle (xs: 'a list) =
  let shuffled = Array.zeroCreate<'a>(xs.Length)
  for i = 0 to xs.Length - 1 do
    let j = rnd.Next(i + 1)
    if i <> j then shuffled.[i] <- shuffled.[j]
    shuffled.[j] <- xs.[i]
  shuffled |> Array.toList


type Model =
  { Count: int 
    Text: string
    RandomList: (Guid * int) list
    LongList: int list }

let addRandom (xs: (Guid * int) list) =
  if rnd.Next(2) % 2 = 0 then xs
  else
    let newItem = (Guid.NewGuid(), rnd.Next(100))
    newItem :: xs

let removeRandom xs =
  if List.isEmpty xs then xs
  else
    if rnd.Next(2) % 2 = 0 then 
      List.tail xs
    else xs


type Msg =
  | Increment
  | Decrement
  | Randomize
  | SetText of string
  | Generate

let init () =
  { Count = 0 
    Text = "asdf"
    RandomList = List.init 10 (fun i ->
      (Guid.NewGuid(), rnd.Next(100))
    ) 
    LongList = []
  }


let longList = [1..1000]


let update msg model =
  match msg with
  | Increment -> { model with Count = model.Count + 1 }
  | Decrement -> { model with Count = model.Count - 1 }
  | Randomize ->
      { model with 
          RandomList =
            model.RandomList
            |> shuffle
            |> removeRandom
            |> addRandom
            |> shuffle
            |> removeRandom
            |> addRandom
            |> shuffle
            |> removeRandom
            |> addRandom
            |> shuffle
      }
  | SetText s -> { model with Text = s }
  | Generate -> { model with LongList = longList }


let setDefaultButtonStyle (btn: Button) =
  btn.Width <- 200.
  btn.Height <- 50.

let setDefaultWindowStyle (win: Window) =
  win.TextElement_Foreground <- SolidColorBrush(InitialView = getResource "MaterialDesignBody")
  win.TextElement_FontWeight <- FontWeights.Regular
  win.TextElement_FontSize <- 13.
  win.TextOptions_TextFormattingMode <- TextFormattingMode.Ideal
  win.TextOptions_TextRenderingMode <- TextRenderingMode.Auto
  win.Background <- SolidColorBrush(InitialView = getResource "MaterialDesignPaper")
  win.FontFamily <- getResource "MaterialDesignFont"


let dpRef = ViewRef<Controls.DockPanel>()


let view model dispatch =
  // Syntax 1 - passing prop setter lambdas to constructor.
  // This makes it easy to discover properties since you can dot into them.
  // It also makes it easy to factor away common stuff such as "styles".
  // In general it's more flexible, since you can call arbitrary functions
  // on the created element.
  [
    yield Window <| fun w ->
      setDefaultWindowStyle w
      w.Content <-
        DockPanel <| fun dp ->
          dp.Ref <- dpRef
          dp.Children <- [
            Button(fun btn ->
              setDefaultButtonStyle btn
              btn.Style <- getResource "MaterialDesignFlatButton"
              // Attached properties work just like normal properties
              btn.DockPanel_Dock <- Controls.Dock.Right
              btn.Content <- TextBlock(fun tb -> 
                tb.Text <- "Increment"
              )
              btn.Click <- fun _ -> dispatch Increment
              if model.Count >= 10 then
                btn.IsEnabled <- false
            )

            lazyWith (=) model.Count (fun count ->
              TextBlock(fun tb ->
                tb.DockPanel_Dock <- Controls.Dock.Right
                tb.Text <- string count
              )
            )

            TextBox(fun tb ->
              tb.Text <- model.Text
              tb.TextChanged <- (fun ev -> SetText ev.Text |> dispatch)
              tb.Error <- if model.Count > 3 then Some "Too high" else None
            )

            // With this syntax you can also use bacwards pipe to avoid parantheses
            Button <| fun btn ->
              setDefaultButtonStyle btn
              btn.DockPanel_Dock <- Dock.Right
              btn.Content <- TextBlock <| fun tb -> 
                tb.Text <- "Decrement"
              btn.Click <- fun _ -> dispatch Decrement
              if model.Count <= 0 then
                btn.IsEnabled <- false

            Button <| fun btn ->
              setDefaultButtonStyle btn
              btn.DockPanel_Dock <- Dock.Left
              btn.Content <- TextBlock <| fun tb -> 
                tb.Text <- "Randomize"
              btn.Click <- fun _ -> dispatch Randomize

            // Syntax 2 - normal property initializers, slightly less verbose
            StackPanel(
              Children = (model.RandomList |> List.map (fun (key, i) -> 
                // You can even mix and match in a single constructor
                upcast TextBlock(
                  (fun tb -> tb.Text <- string i),
                  Key = string key
                )
              ))
            )

            CheckBox(fun cb ->
              cb.IsChecked <- 
                if model.Count > 5 then Some true
                elif model.Count < 3 then Some false
                else None
            )
          ]

    if model.Count > 2 then
      yield Window(
        Height = 200.,
        Content = "Test"
      )
  ]


[<EntryPoint; STAThread>]
let main argv =
  let app = Application()
  app.ShutdownMode <- ShutdownMode.OnMainWindowClose

  /// Load material design styles
  app.Resources.MergedDictionaries.Add(ResourceDictionary(Source = Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml")))
  app.Resources.MergedDictionaries.Add(ResourceDictionary(Source = Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Defaults.xaml")))
  app.Resources.MergedDictionaries.Add(ResourceDictionary(Source = Uri("pack://application:,,,/MaterialDesignColors;component/Themes/Recommended/Primary/MaterialDesignColor.Blue.xaml")))
  app.Resources.MergedDictionaries.Add(ResourceDictionary(Source = Uri("pack://application:,,,/MaterialDesignColors;component/Themes/Recommended/Accent/MaterialDesignColor.Lime.xaml")))
  app.Resources.MergedDictionaries.Add(ResourceDictionary(Source = Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.PopupBox.xaml")))

  Program.mkSimple init update view
  |> Program.withConsoleTrace
  |> Program.runWindows
