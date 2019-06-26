module Write

open System.IO
open Domain


[<AutoOpen>]
module Helpers =

  let defaultValueFieldName propName =
    sprintf "_Def%s" propName

  let propBackingFieldName propName =
    sprintf "_%s" propName

/// Writes the field used for obtaining the default value of a simple property.
let defaultValueField (w: StringWriter) = function
  | Prop.Single ({ DependencyProp = Some dp } as p) ->
      let defField = defaultValueFieldName p.NodeName
      w.printfn "  static let %s = %s.DefaultMetadata.DefaultValue :?> %s" defField dp p.RealType
  | Prop.Single _ | Prop.Collection _ | Prop.SelfCollection _ | Prop.Event _ | Prop.Error _ -> ()


/// Writes the backing field and definition of a property.
let propBackingFieldAndDefinition (w: StringWriter) = function
  | Prop.Single p ->
      let backingName = propBackingFieldName p.NodeName
      w.printfn "  [<DefaultValue>] val mutable private %s: %s voption" backingName p.NodeType
      w.printfn "  member this.%s with set x = this.%s <- ValueSome x" p.NodeName backingName
      w.printfn ""
  | Prop.Collection p ->
      let backingName = propBackingFieldName p.NodeName
      w.printfn "  [<DefaultValue>] val mutable private %s: %s list voption" backingName p.NodeItemType
      w.printfn "  member this.%s with set x = this.%s <- ValueSome x" p.NodeName backingName
      w.printfn ""
  | Prop.SelfCollection p ->
      let backingName = propBackingFieldName p.NodeName
      w.printfn "  [<DefaultValue>] val mutable private %s: %s list voption" backingName p.NodeItemType
      w.printfn "  member this.%s with set x = this.%s <- ValueSome x" p.NodeName backingName
      w.printfn ""
  | Prop.Event p ->
      let backingName = propBackingFieldName p.NodeName
      w.printfn "  [<DefaultValue>] val mutable private %s: EventHandlerWrapper<%s> voption" backingName p.EventArgsType
      w.printfn "  member this.%s with set x = this.%s <- EventHandlerWrapper(this, x) |> ValueSome" p.NodeName backingName
      w.printfn ""
  | Prop.Error p ->
      let backingName = propBackingFieldName p.NodeName
      w.printfn "  [<DefaultValue>] val mutable private %s: string option voption" backingName
      w.printfn "  member this.%s with set x = this.%s <- ValueSome x" p.NodeName backingName
      w.printfn ""


/// Writes the code that sets the value for a property when initially rendered.
let propInitialSet (w: StringWriter) = function
  | Prop.Single p ->
      let backingName = propBackingFieldName p.NodeName
      let extraTransform = 
        match p.ExtraMapBeforeSet with
        | None -> ""
        | Some f -> sprintf " |> %s" f
      match p.NodeTypeIsNodeType with
      | None -> w.printfn "    this.%s |> ValueOption.iter (fun x -> v.%s <- (match x with :? Node as n -> n.RenderNew () |> box | _ -> x))" backingName p.RealName
      | Some true -> w.printfn "    this.%s |> ValueOption.iter (fun x -> v.%s <- x.RenderNew () :?> %s)" backingName p.RealName p.RealType
      | Some false -> w.printfn "    this.%s |> ValueOption.iter (fun x -> v.%s <- x%s)" backingName p.RealName extraTransform
  | Prop.Collection p ->
      let backingName = propBackingFieldName p.NodeName
      match p.NodeItemTypeIsNodeType with
      | None -> w.printfn "    this.%s |> ValueOption.iter (fun xs -> xs |> List.iter (fun x -> (match x with :? Node as n -> n.RenderNew () |> box | _ -> x) :?> %s |> v.%s.Add |> ignore))" backingName p.RealItemType p.RealName
      | Some true -> w.printfn "    this.%s |> ValueOption.iter (fun xs -> xs |> List.iter (fun x -> x.RenderNew () :?> %s |> v.%s.Add |> ignore))" backingName p.RealItemType p.RealName
      | Some false -> w.printfn "    this.%s |> ValueOption.iter (fun xs -> xs |> List.iter (v.%s.Add >> ignore))" backingName p.RealName
  | Prop.SelfCollection p ->
      let backingName = propBackingFieldName p.NodeName
      match p.NodeItemTypeIsNodeType with
      | None -> w.printfn "    this.%s |> ValueOption.iter (fun xs -> xs |> List.iter (fun x -> (match x with :? Node as n -> n.RenderNew () |> box | _ -> x) :?> %s |> v.Add |> ignore))" backingName p.RealItemType
      | Some true -> w.printfn "    this.%s |> ValueOption.iter (fun xs -> xs |> List.iter (fun x -> x.RenderNew () :?> %s |> v.Add |> ignore))" backingName p.RealItemType
      | Some false -> w.printfn "    this.%s |> ValueOption.iter (fun xs -> xs |> List.iter (v.Add >> ignore))" backingName
  | Prop.Event p ->
      let backingName = propBackingFieldName p.NodeName
      w.printfn "    this.%s |> ValueOption.iter (fun x -> x.Subscription <- v.%s.Subscribe x.Fn)" backingName p.RealName
  | Prop.Error p ->
      let backingName = propBackingFieldName p.NodeName
      w.printfn "    this.%s |> ValueOption.iter (fun x -> this.SetError(v, x))" backingName


/// Writes the code that incrementally updates a property.
let propUpdate (w: StringWriter) = function
  | Prop.Single p ->
      let backingName = propBackingFieldName p.NodeName
      let defValExpr = 
        if p.DependencyProp.IsSome
        then defaultValueFieldName p.NodeName
        else sprintf "Unchecked.defaultof<%s>" p.RealType
      let extraTransform = p.ExtraMapBeforeSet |> Option.defaultValue "id"
      w.printfn "    updateValue %s prev.%s this.%s %s (fun x -> v.%s <- x)" defValExpr backingName backingName extraTransform p.RealName
  | Prop.Collection p ->
      let backingName = propBackingFieldName p.NodeName
      w.printfn "    updateChildren prev.%s this.%s v.%s" backingName backingName p.RealName
  | Prop.SelfCollection p ->
      let backingName = propBackingFieldName p.NodeName
      w.printfn "    updateChildren prev.%s this.%s v" backingName backingName
  | Prop.Event p ->
      let backingName = propBackingFieldName p.NodeName
      w.printfn "    updateFn prev.%s this.%s v.%s" backingName backingName p.RealName
  | Prop.Error p ->
      let backingName = propBackingFieldName p.NodeName
      w.printfn "    this.%s |> ValueOption.iter (fun x -> this.SetError(v, x))" backingName


let nodeType (w: StringWriter) (t: NodeType) =
  if t.IsAbstract then 
    w.printfn "[<AbstractClass>]"
    w.printfn "type %s() =" t.NameWithConstraints
  else
    w.printfn "type %s(setProps: %s -> unit) as this =" t.NameWithConstraints t.Name
  w.printfn "  inherit %s()" t.BaseNodeType
  w.printfn ""
  t.Props |> List.iter (defaultValueField w)
  w.printfn ""
  if not t.IsAbstract then
    w.printfn "  do setProps this"
    w.printfn ""
    w.printfn "  new() = %s(fun _ -> ())" t.Name
    w.printfn ""
    w.printfn "  [<DefaultValue>] val mutable internal __View: %s voption" t.RealType
    w.printfn ""
    w.printfn "  [<DefaultValue>] val mutable private _InitialView: %s voption" t.RealType
    w.printfn "  member this.InitialView with set x = this._InitialView <- ValueSome x"
    w.printfn ""
  t.Props |> List.iter (propBackingFieldAndDefinition w)
  w.printfn "  [<DefaultValue>] val mutable _Ref: ViewRef<%s> voption" t.RealType
  w.printfn "  member this.Ref with set x = this._Ref <- ValueSome x"
  w.printfn ""
  w.printfn "  member internal this.SetInitialProps (v: %s) =" t.RealType
  w.printfn "    base.SetInitialProps v"
  w.printfn "    this.SilenceEvents <- true"
  t.Props |> List.iter (propInitialSet w)
  w.printfn "    this.SilenceEvents <- false"
  w.printfn ""
  w.printfn "  member internal this.UpdateProps (prev: %s, v: %s) =" t.Name t.RealType
  w.printfn "    base.UpdateProps (prev, v)"
  w.printfn "    this.SilenceEvents <- true"
  t.Props |> List.iter (propUpdate w)
  w.printfn "    this.SilenceEvents <- false"
  if not t.IsAbstract then
    w.printfn ""
    w.printfn "  override this.RenderNew () ="
    w.printfn "    let v = this._InitialView |> ValueOption.defaultValue (new %s())" t.RealType
    w.printfn "    this.__View <- ValueSome v"
    w.printfn "    this._Ref |> ValueOption.iter (fun x -> x.Set v)"
    w.printfn "    this.SetInitialProps v"
    w.printfn "    upcast v"
    w.printfn ""
    w.printfn "  override this.UpdateIncremental (prev: Node) ="
    w.printfn "    if System.Object.ReferenceEquals(this, prev) then ValueNone"
    w.printfn "    else"
    w.printfn "      match prev with"
    w.printfn "      | :? %s as prev ->" t.Name
    w.printfn "          match prev.__View with"
    w.printfn "          | ValueNone -> "
    w.printfn "              this.RenderNew () |> ValueSome"
    w.printfn "          | ValueSome v ->"
    w.printfn "              this.__View <- ValueSome v"
    w.printfn "              this.UpdateProps (prev, v)"
    w.printfn "              ValueNone"
    w.printfn "      | _ -> this.RenderNew () |> ValueSome"
    w.printfn ""
    w.printfn "  override this.UpdateIncrementalAndReturn (prev: Node) = "
    w.printfn "    this.UpdateIncremental prev |> ignore"
    w.printfn "    upcast this.__View.Value"
  w.printfn ""
  w.printfn ""


let attachedPropDefaultValue (w: StringWriter) (p: AttachedProp) =
  let defField = defaultValueFieldName p.NodeName
  w.printfn "    let %s = %s.DefaultMetadata.DefaultValue" defField p.DependencyProp


let attachedPropSetter (w: StringWriter) (p: AttachedProp) =
    w.printfn "    let %s = RefEqWrapper (fun (target: obj) (value: obj) -> " p.NodeName
    w.printfn "      %s(unbox<%s> target, unbox<%s> value))" p.StaticSetter p.RealTargetType p.RealValueType
    w.printfn ""


let attachedDefinitionSetterModule (w: StringWriter) (nodeDeclaringType: string, ps: AttachedProp list) =
  w.printfn "  module internal %s =" nodeDeclaringType
  w.printfn ""
  ps |> List.iter (attachedPropDefaultValue w)
  w.printfn ""
  ps |> List.iter (attachedPropSetter w)
  w.printfn ""


let attachedTargetExtensionSetter (w: StringWriter) (p: AttachedProp) =
  let mapExpr =
    p.ExtraMapBeforeSet
    |> Option.map (sprintf " |> %s")
    |> Option.defaultValue ""
  w.printfn "    member this.%s_%s" p.NodeDeclaringType p.NodeName
  w.printfn "      with set (value: %s) = this.__AttachedProps.Add(%s.%s, (value%s |> box, %s.%s))" p.NodeValueType p.NodeDeclaringType p.NodeName mapExpr p.NodeDeclaringType (defaultValueFieldName p.NodeName)
  w.printfn ""


let attachedPropExtensions (w: StringWriter) (nodeTargetType: string, ps: AttachedProp list)  =
  w.printfn "  type %s with" nodeTargetType
  w.printfn ""
  ps |> List.sortBy (fun p -> p.NodeDeclaringType, p.NodeName) |> List.iter (attachedTargetExtensionSetter w)
  w.printfn ""


let prelude (w: StringWriter) (ctx: GenerationContext) =
  w.printfn "namespace rec %s" ctx.Namespace
  w.printfn ""
  w.printfn "#nowarn \"66\"  // \"This upcast is unnecessary - the types are identical\""
  w.printfn "#nowarn \"67\"  // \"This type test or downcast will always hold\""


let everything (w: StringWriter) (ctx: GenerationContext) =
  prelude w ctx
  w.printfn ""
  w.printfn ""
  ctx.Types |> List.iter (nodeType w)
  if not ctx.AttachedProps.IsEmpty then
    let withNameSpace (typeName: string) =
      if ctx.Types |> List.exists (fun t -> t.Name = typeName)
      then sprintf "%s.%s" ctx.Namespace typeName
      else sprintf "Elmish.WPF.Dynamic.%s" typeName
    w.printfn "[<AutoOpen>]"
    w.printfn "module AttachedProps ="
    w.printfn ""
    ctx.AttachedProps
      |> List.groupBy (fun p -> p.NodeDeclaringType)
      |> List.sortBy fst
      |> List.iter (attachedDefinitionSetterModule w)
    w.printfn ""
    ctx.AttachedProps
      |> List.groupBy (fun p -> withNameSpace p.NodeTargetType)
      |> List.sortBy fst
      |> List.iter (attachedPropExtensions w)
