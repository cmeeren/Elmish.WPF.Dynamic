namespace Elmish.WPF.Dynamic

open System
open System.Collections
open System.Collections.Generic
open System.Windows
open System.Windows.Data


[<ReferenceEquality>]
[<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
type RefEqWrapper<'a> = RefEqWrapper of 'a with
  member this.Wrapped = let (RefEqWrapper x) = this in x


type ViewRef<'a when 'a : null and 'a : not struct>() = 
  let handle = System.WeakReference<'a>(null)

  [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
  member __.Set target = handle.SetTarget(target)

  member __.TryValue =
    match handle.TryGetTarget () with 
    | true, res -> Some res
    | false, _ -> None

  member __.Iter f =
    match handle.TryGetTarget () with 
    | true, res -> f res
    | false, _ -> ()



[<AbstractClass; AllowNullLiteral>]
type Node() =

  let mutable errorHelper = ValueNone
  
  // Keys = attached prop setters
  // Values = value * defaultValue
  let attachedProps = Dictionary<RefEqWrapper<obj -> obj -> unit>, obj * obj>()

  [<DefaultValue>] val mutable internal _Key: string voption
  member this.Key with set v = this._Key <- ValueSome v

  [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
  member val SilenceEvents = false with get, set

  [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
  member __.__AttachedProps = attachedProps

  [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
  member __.SetError(v: FrameworkElement, err) =
    let errHelper =
      match errorHelper with
      | ValueSome helper -> helper
      | ValueNone ->
          let helper = ErrorHelper()
          let binding = Binding()
          binding.Source <- helper
          binding.Path <- PropertyPath("Dummy")
          // We hijack the DataContext binding to set the control's validation error.
          // Might need to change this if we need to use DataContext to get virtualization
          // to work like Fabulous does.
          BindingOperations.SetBinding(v, FrameworkElement.DataContextProperty, binding) |> ignore
          errorHelper <- ValueSome helper
          helper
    errHelper.Error <- err

  member internal this.SetPropsFrom(v: obj) = 
    ()


  [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
  member this.SetInitialProps (v: obj) =
    this.SilenceEvents <- true
    this.__AttachedProps |> Seq.iter (fun kvp -> 
      let setValue = kvp.Key.Wrapped v
      let (value, defVal) = kvp.Value
      match value with
      | :? Node as n -> n.RenderNew () |> setValue
      | _ -> setValue value
    )
    this.SilenceEvents <- false


  [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
  member this.UpdateProps (prev: Node, v: obj) =
    this.SilenceEvents <- true
    [prev.__AttachedProps; this.__AttachedProps]
    |> Seq.concat
    |> Seq.distinctBy (fun kvp -> kvp.Key)
    |> Seq.iter (fun kvp ->
        let setValue = kvp.Key.Wrapped v
        match prev.__AttachedProps.TryFind kvp.Key, this.__AttachedProps.TryFind kvp.Key with
        | ValueNone, ValueNone -> ()
        | ValueSome (_, defVal), ValueNone -> setValue defVal
        | ValueSome (:? Node as prev, _), ValueSome (:? Node as curr, _) ->
            curr.UpdateIncremental prev |> ValueOption.iter setValue
        | ValueSome (prev, _), ValueSome (curr, _) when prev = curr -> ()
        | _, ValueSome (:? Node as curr, _) -> curr.RenderNew () |> setValue
        | _, ValueSome (curr, _) -> setValue curr
    )
    this.SilenceEvents <- false

  /// Renders and returns a new view
  [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
  abstract RenderNew : unit -> DependencyObject

  /// Attempts to incrementally update an existing view. If successful,
  /// returns ValueNone. Otherwise renders and returns the new view.
  [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
  abstract UpdateIncremental : prev: Node -> DependencyObject voption

  /// Attempts to incrementally update an existing view, falling back to
  /// rendering a new view. Returns the view regardless of the result.
  [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
  abstract UpdateIncrementalAndReturn : prev: Node -> DependencyObject



[<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
type EventHandlerWrapper<'a>(owner: Node, f: 'a -> unit) =
  let id = f.GetType()
  let fn = fun x -> if owner.SilenceEvents then () else f x

  member __.Fn = fn

  member __.Id = id

  member val Subscription = 
    { new IDisposable with member __.Dispose () = () }
    with get, set


[<AutoOpen>]
module Extensions =

  type System.Windows.Controls.TextChangedEventArgs with
    
    /// Gets the current text of the source TextBox. Throws if the source is
    /// not a TextBox.
    member this.Text =
      match this.Source with
      | :? System.Windows.Controls.TextBox as x -> x.Text
      | _ -> failwith "This property can only be used with a TextBox"


[<AutoOpen>]
module Helpers =


  /// Retrieves a resource of the specified type from the current application.
  /// Throws if the resource type doesn't match the specified/inferred type.
  /// Returns null if the resource isn't found.
  let getResource<'a> (key: string) =
    Application.Current.TryFindResource(key) :?> 'a


  /// Reference/physical equality for reference types. Alias for
  /// LanguagePrimitives.PhysicalEquality. Also see elmEq.
  let refEq = LanguagePrimitives.PhysicalEquality

  
  /// Memberwise equality where value-typed members and string members are
  /// compared using structural comparison (the standard F# (=) operator),
  /// and reference-typed members (except strings) are compared using reference
  /// equality.
  ///
  /// This is a useful default for lazy since all parts of the Elmish model
  /// (i.e., all members of the arguments to this function) are normally immutable.
  /// For a direct reference equality check (not memberwise), see refEq (which
  /// should be used when passing a single non-string reference type from the model).
  let elmEq<'a> : 'a -> 'a -> bool =
    ElmEq<'a>.Eq
  

  /// Memoizes part of the view calculation and reconciliation.
  /// 
  /// The specified equality comparer will be used to compare the models.
  /// Good candidates are elmEq (if depending on multiple fields), refEq
  /// (if depending on a single reference type field), and (=) (if depending
  /// on a single value type field or otherwise needing structural equality).
  ///
  /// It is not necessary to define the function separately; this function
  /// works correctly if used inline, too.
  let lazyWith eq (m: 'model) (f: 'model -> 'node) : 'node =
    let key = f.GetType()
    let dict = Memoizations<Type, 'model, 'node>.Memoized
    let calc () =
      let res = f m
      dict.[key] <- (m, res)
      res
    match dict.TryGetValue key with
    | true, (model, node) when eq model m -> node
    | _ -> calc ()


[<AutoOpen>]
module Reconciliation =


  [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
  let updateValue 
      (defVal: 'raw)
      (prev: 'a voption)
      (curr: 'a voption)
      (map: 'b -> 'raw)
      (setValue: 'raw -> unit) =
    match ValueOption.map box prev, ValueOption.map box curr with
    | ValueNone, ValueNone -> 
        ()
    | ValueSome _, ValueNone -> 
        setValue defVal
    | ValueSome (:? Node as prev), ValueSome (:? Node as curr) ->
        curr.UpdateIncremental prev |> ValueOption.iter (unbox<'b> >> map >> setValue)
    | ValueSome prev, ValueSome curr when prev = curr -> 
        ()
    | _, ValueSome (:? Node as curr) -> 
        curr.RenderNew () |> unbox<'b> |> map |> setValue
    | _, ValueSome curr -> 
        curr |> unbox<'b> |> map |> setValue

  let updateFn (prev: EventHandlerWrapper<'a> voption) (curr: EventHandlerWrapper<'a> voption) (event: IEvent<'sender, 'a>) =
    match prev, curr with
    | ValueNone, ValueNone -> 
        ()
    | ValueSome prev', ValueSome curr' when prev'.Id = curr'.Id -> 
        curr'.Subscription <- prev'.Subscription
    | ValueSome prev', ValueSome curr' -> 
        prev'.Subscription.Dispose ()
        curr'.Subscription <- event.Subscribe curr'.Fn
    | ValueSome prev, ValueNone -> 
        prev.Subscription.Dispose ()
    | ValueNone, ValueSome curr -> 
        curr.Subscription <- event.Subscribe curr.Fn


  [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
  let updateChildren (prevOpt: 'a list voption) (currOpt: 'a list voption) (coll: IList) =
    match prevOpt, currOpt with
    | ValueNone, ValueNone -> ()
    | ValueSome _, ValueNone -> coll.Clear ()
    | ValueNone, ValueSome currs -> 
        currs |> List.iter (fun curr ->
          match box curr with
          | :? Node as n -> n.RenderNew () |> coll.Add |> ignore
          | _ -> curr |> coll.Add |> ignore
        )
    | ValueSome prevs, ValueSome currs ->
        let prevKeyed = Dictionary<string, Node>()
        let prevUnkeyed = ResizeArray<Node>(0)  // We hope everything is keyed, so don't allocate anything yet
        prevs |> List.iter (fun prev ->
          match box prev with
          | :? Node as node ->
              match node._Key with 
              | ValueSome k -> prevKeyed.Add (k, node) 
              | ValueNone ->
                  // Since we're here we assume nothing is keyed, so set capacity to fit all elements of prevs
                  if prevUnkeyed.Count = 0 then prevUnkeyed.Capacity <- prevs.Length
                  prevUnkeyed.Add node
          | _ -> ()
        )

        let popPrevKeyed key =
          let prevOpt = prevKeyed.TryFind key
          if prevOpt.IsSome then prevKeyed.Remove key |> ignore
          prevOpt

        let popPrevUnkeyed () =
          if prevUnkeyed.Count = 0 then ValueNone
          else
            let prev = prevUnkeyed.[0]
            prevUnkeyed.RemoveAt 0
            ValueSome prev

        let setOrAdd i v =
          if coll.Count > i then
            if coll.[i] = v then ()
            else
              coll.Remove(v)
              coll.Insert(i, v)
          else
            coll.Add v |> ignore

        let mutable currLength = 0  // probably more effective than List.length?
        currs |> List.iteri (fun iCurr curr ->
          currLength <- iCurr + 1
          match box curr with
          | :? Node as node ->
              match node._Key |> ValueOption.bind popPrevKeyed with
              | ValueSome prev ->
                  // Previous item found with the same key - do incremental update
                  node.UpdateIncrementalAndReturn prev |> setOrAdd iCurr
              | ValueNone ->
                  // No previous item found with same key - pick first non-key item and hope it can be updated
                  match popPrevUnkeyed () with
                  | ValueSome prev -> node.UpdateIncrementalAndReturn prev |> setOrAdd iCurr
                  | ValueNone -> node.RenderNew () |> setOrAdd iCurr
          | _ -> curr |> setOrAdd iCurr
        )

        if coll.Count > currLength then
          [coll.Count - 1 .. -1 .. currLength] |> List.iter coll.RemoveAt
