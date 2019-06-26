module Elmish.WPF.Dynamic.Program

open System.Collections.Generic
open System.Windows
open Elmish


let getApp () =
  if isNull Application.Current then Application() else Application.Current


let private uiDispatch (innerDispatch: Dispatch<'msg>) : Dispatch<'msg> =
  fun msg -> Application.Current.Dispatcher.Invoke(fun () -> innerDispatch msg)


let private runWith setState =
  Program.withSetState setState
  >> Program.withSyncDispatch uiDispatch
  >> Program.run


/// Starts both Elmish and WPF dispatch loops. Blocking function.
let runWindow (program: Program<unit, 'model, 'msg, Elmish.WPF.Dynamic.Window>) =

  let app = getApp ()
  
  let mutable prev : Elmish.WPF.Dynamic.Window voption = ValueNone

  let setState model dispatch =
    let curr = Program.view program model dispatch
    match prev with
    | ValueNone -> (curr.RenderNew () :?> Window).Show()
    | ValueSome prev' -> curr.UpdateIncremental prev' |> ignore
    prev <- ValueSome curr

  program |> runWith setState
  app.Run()
  

/// Starts both Elmish and WPF dispatch loops. Blocking function.
let runWindows (program: Program<unit, 'model, 'msg, Elmish.WPF.Dynamic.Window list>) =

  let app = getApp ()
  
  let mutable prev : Elmish.WPF.Dynamic.Window list voption = ValueNone

  let setState model dispatch =
    let curr = Program.view program model dispatch
    match prev with
    | ValueNone -> curr |> List.iter (fun w -> (w.RenderNew () :?> Window).Show())
    | ValueSome prev' ->

        let prevKeyed = Dictionary<string, Elmish.WPF.Dynamic.Window>()
        let prevUnkeyed = ResizeArray<Elmish.WPF.Dynamic.Window>(0)  // We hope everything is keyed, so don't allocate anything yet
        prev' |> List.iter (fun win ->
          match win._Key with 
          | ValueSome key -> prevKeyed.Add (key, win) 
          | ValueNone ->
              // Since we're here we assume nothing is keyed, so set capacity to fit all elements of prev'
              if prevUnkeyed.Count = 0 then prevUnkeyed.Capacity <- prev'.Length
              prevUnkeyed.Add win
        )

        let popPrevKeyed key =
          let prevOpt = prevKeyed.TryFind key
          if prevOpt.IsSome then prevKeyed.Remove key |> ignore
          prevOpt

        let popPrevUnkeyed () =
          if prevUnkeyed.Count = 0 then ValueNone
          else
            let win = prevUnkeyed.[0]
            prevUnkeyed.RemoveAt 0
            ValueSome win

        curr |> List.iter (fun currWin ->
          match currWin._Key |> ValueOption.bind popPrevKeyed with
          | ValueSome prevWin ->
              // Previous item found with the same key - do incremental update
              currWin.UpdateIncremental prevWin |> ignore  // Safe to ignore since we know it's the same element
          | ValueNone ->
              // No previous item found with same key - pick first non-key item and hope it can be effectively updated
              match popPrevUnkeyed () with
              | ValueSome prevWin -> currWin.UpdateIncremental prevWin |> ignore  // Safe to ignore since we know it's the same element
              | ValueNone -> (currWin.RenderNew () :?> Window).Show()
        )

        if prev'.Length > curr.Length then
          prev' |> List.skip curr.Length |> List.iter (fun w -> w.__View.Value.Close())

    prev <- ValueSome curr

  program |> runWith setState
  app.Run()
  