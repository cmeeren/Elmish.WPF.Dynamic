module Analyze

open System
open Domain
open System.Collections
open System.Reflection
open System.Windows


let simplifyRealTypeName = function 
  | "System.Boolean" -> "bool"
  | "System.Byte" -> "byte"
  | "System.SByte" -> "sbyte"
  | "System.Int16" -> "int16"
  | "System.UInt16" -> "uint16"
  | "System.Int32" -> "int"
  | "System.UInt32" -> "uint32"
  | "System.Int64" -> "int64"
  | "System.UInt64" -> "uint64"
  | "System.Single" -> "float32"
  | "System.Double" -> "float"
  | "System.Decimal" -> "decimal"
  | "System.Char" -> "char"
  | "System.String" -> "string"
  | "System.Object" -> "obj"
  | s -> s

let rec getRealTypeName (t: Type) =
  if t.IsGenericTypeParameter then
    match t.GetGenericParameterConstraints() with
    | [||] -> sprintf "'%s" t.Name
    | [|c|] -> 
        getRealTypeName c
    | _ -> failwith "multiple type constraints not supported"
  elif t.IsGenericType then
    let baseName =
      if isNull t.FullName
      then t.Name.Substring(0, t.Name.IndexOf('`'))
      else t.FullName.Substring(0, t.FullName.IndexOf('`'))
    let genericArguments = t.GetGenericArguments() |> Array.map (fun t -> getRealTypeName t)
    sprintf "%s<%s>" baseName (String.concat ", " genericArguments)
  else
    t.FullName |> simplifyRealTypeName


let rec getNodeTypeName (t: Type) =
  if t.IsGenericTypeParameter then
    sprintf "'%s" t.Name
  elif t.IsGenericType then
    let baseName = t.Name.Substring(0, t.Name.IndexOf('`')) 
    let genericArguments = t.GetGenericArguments() |> Array.map (fun t -> getNodeTypeName t)
    sprintf "%s<%s>" baseName (String.concat ", " genericArguments)
  else
    t.Name


let rec getNodeTypeNameWithConstraints (t: Type) =
  if t.IsGenericTypeParameter then
    match t.GetGenericParameterConstraints() with
    | [||] -> sprintf "'%s" t.Name
    | [|c|] -> 
        let constraintType =
          if c.IsAssignableTo<DependencyObject>() then getNodeTypeName c else getRealTypeName c
        sprintf "'%s when '%s :> %s" t.Name t.Name constraintType
    | _ -> failwith "multiple type constraints not supported"
  elif t.IsGenericType then
    let baseName = t.Name.Substring(0, t.Name.IndexOf('`')) 
    let genericArguments = t.GetGenericArguments() |> Array.map (fun t -> getNodeTypeNameWithConstraints t)
    sprintf "%s<%s>" baseName (String.concat ", " genericArguments)
  else
    t.Name


let isCollectionProp (pi: PropertyInfo) =
  // Currently reconciliation only supports IList; can probably change easily if needed
  pi.PropertyType.IsAssignableTo<IList>()
  && not (pi.PropertyType.IsGenericType && pi.PropertyType.GetGenericTypeDefinition() = typedefof<System.Collections.ObjectModel.ReadOnlyCollection<_>>)
  // If the collection is itself a DependencyObject, we want to use it as a Simple prop
  // since it may have other useful props
  && not <| pi.PropertyType.IsAssignableTo<DependencyObject>()


let getCollectionProp getType (cfg: Config.Root) (pi: PropertyInfo) =
  match cfg.GetPropertyCollectionItemType getType pi with
  | Some itemType ->
      Prop.Collection {
        NodeName = pi.Name
        RealName = pi.Name
        NodeItemType =
          if itemType.IsAssignableTo<DependencyObject>()
          then getNodeTypeName itemType
          else getRealTypeName itemType
        RealItemType = getRealTypeName itemType
        NodeItemTypeIsNodeType =
          if itemType.IsAssignableTo<DependencyObject>() then Some true
          elif itemType.IsAssignableFrom(typeof<DependencyObject>) then None  // Basically just obj
          else Some false
      } |> Some
  | None -> 
      // TODO: use obj?
      printfn "WARNING: Collection property %s.%s with type %s has no configured or inferrable item type; skipping" pi.DeclaringType.FullName pi.Name pi.PropertyType.FullName
      None


let isSelfCollectionProp (pi: PropertyInfo) =
  // If it's an indexer property, we treat that as the property to use for
  // setting the collection items
  pi.GetIndexParameters().Length > 0


let getSelfCollectionProp getType cfg (pi: PropertyInfo) =
  if not <| pi.DeclaringType.IsAssignableTo<IList>() then 
    printfn "WARNING: Encountered indexer property %s.%s on non-IList class type; skipping" pi.DeclaringType.FullName pi.Name
    None
  else
    match getCollectionItemType getType cfg pi.DeclaringType with
    | Some itemType ->
        Prop.SelfCollection {
          NodeName = "Items"
          NodeItemType =
            if itemType.IsAssignableTo<DependencyObject>()
            then getNodeTypeName itemType
            else getRealTypeName itemType
          RealItemType = getRealTypeName itemType
          NodeItemTypeIsNodeType =
            if itemType.IsAssignableTo<DependencyObject>() then Some true
            elif itemType.IsAssignableFrom(typeof<DependencyObject>) then None  // Basically just obj
            else Some false
        } |> Some
    | None ->
        // TODO: use obj?
        printfn "WARNING: Self collection property %s.%s with type %s has no configured or inferrable item type; skipping" pi.DeclaringType.FullName pi.Name pi.PropertyType.FullName
        None


let getSingleProp (pi: PropertyInfo) =
  Prop.Single {
    NodeName = pi.Name
    RealName = pi.Name
    NodeType =
      if pi.PropertyType.IsAssignableTo<DependencyObject>() then 
        getNodeTypeName pi.PropertyType
      elif pi.PropertyType.IsGenericType && pi.PropertyType.GetGenericTypeDefinition() = typedefof<Nullable<_>> then
        (pi.PropertyType.GetGenericArguments() |> Array.head |> getRealTypeName) + " option"
      else 
        getRealTypeName pi.PropertyType
    RealType = getRealTypeName pi.PropertyType
    NodeTypeIsNodeType =
      if pi.PropertyType.IsAssignableTo<DependencyObject>() then Some true
      elif pi.PropertyType.IsAssignableFrom(typeof<DependencyObject>) then None  // Basically just obj
      else Some false
    ExtraMapBeforeSet =
      if pi.PropertyType.IsGenericType && pi.PropertyType.GetGenericTypeDefinition() = typedefof<Nullable<_>>
      then Some "Option.toNullable"
      else None
    DependencyProp = 
      let assumedName = pi.Name + "Property"
      let dependencyPropInfo =
        pi.DeclaringType.GetField(
          assumedName,
          BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.DeclaredOnly)
      match dependencyPropInfo with
      | null -> None
      | depPi when depPi.FieldType = typeof<DependencyProperty> -> 
          sprintf "%s.%s" (getRealTypeName pi.DeclaringType) assumedName |> Some
      | _ -> None
  } |> Some


let getEventProp (ei: EventInfo) =
  let eventArgsType = 
    ei.EventHandlerType.GetMethod("Invoke").GetParameters().[1].ParameterType
  Prop.Event {
    NodeName = ei.Name
    RealName = ei.Name
    EventArgsType = getRealTypeName eventArgsType
  } |> Some


let getNonEventProp getType cfg (pi: PropertyInfo) =
  if isSelfCollectionProp pi then getSelfCollectionProp getType cfg pi
  elif isCollectionProp pi then getCollectionProp getType cfg pi
  else getSingleProp pi


let getAttachedProp (setter: MethodInfo) =
  {
    NodeName = setter.Name.Substring(3)
    NodeDeclaringType = getNodeTypeName setter.DeclaringType
    NodeTargetType = getNodeTypeName setter.AttachedPropTargetType
    RealTargetType = getRealTypeName setter.AttachedPropTargetType
    NodeValueType = 
      let t = setter.AttachedPropValueType
      if t.IsAssignableTo<DependencyObject>() then 
        getNodeTypeName t
      elif t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<Nullable<_>> then
        (t.GetGenericArguments() |> Array.head |> getRealTypeName) + " option"
      else
        getRealTypeName t
    RealValueType = getRealTypeName setter.AttachedPropValueType
    StaticSetter =
      sprintf "%s.%s"
        (getRealTypeName setter.DeclaringType)
        setter.Name
    ExtraMapBeforeSet =
      let t = setter.AttachedPropValueType
      if t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<Nullable<_>>
      then Some "Option.toNullable"
      else None
    DependencyProp =
      sprintf "%s.%sProperty"
        (getRealTypeName setter.DeclaringType)
        (setter.Name.Substring(3))
  } |> Some


let shouldUseType getType (cfg: Config.Root) (t: Type) =
  t.IsVisible
  && t.IsAssignableTo<DependencyObject>()
  && not t.IsObsolete
  && not (cfg.IsTypeGloballyIgnored t)
  && not (t.InheritanceHierarchy |> List.exists (fun t -> cfg.IsTypeGloballyIgnored t))
  && not (getCollectionItemType getType cfg t |> Option.exists (fun t -> cfg.IsTypeGloballyIgnored t))


let shouldUseAttachedPropSourceType (cfg: Config.Root) (t: Type) =
  t.IsVisible
  && not t.IsObsolete
  && not (cfg.IsTypeGloballyIgnored t)
  && not (t.InheritanceHierarchy |> List.exists (fun t -> cfg.IsTypeGloballyIgnored t))


let shouldUseEventProperty (cfg: Config.Root) (ei: EventInfo) =
  not ei.IsObsolete
  && not (cfg.IsPropertyIgnored ei)


let shouldUseNonEventProperty getType (cfg: Config.Root) (pi: PropertyInfo) =
  not pi.IsObsolete
  && not (cfg.IsPropertyIgnored pi)
  && not (cfg.IsPropertyTypeGloballyIgnored getType pi.PropertyType)
  && (isCollectionProp pi || isSelfCollectionProp pi || pi.IsSettable)


let shouldUseAttachedProperty getType (cfg: Config.Root) (staticSetter: MethodInfo) =
  not staticSetter.IsObsolete
  && not (cfg.IsPropertyTypeGloballyIgnored getType staticSetter.AttachedPropValueType)


let getNodeType getType cfg (t: Type) =
  {
    Name = getNodeTypeName t
    NameWithConstraints = getNodeTypeNameWithConstraints t
    BaseNodeType = 
      match t.BaseType.Name with
      | "DispatcherObject" -> "Node"
      | _ -> getNodeTypeName t.BaseType
    IsAbstract = t.IsEffectivelyAbstract
    RealType = getRealTypeName t
    Props =
      let nonEventProps =
        t.GetProperties(BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.DeclaredOnly)
        |> Seq.filter (shouldUseNonEventProperty getType cfg)
        |> Seq.choose (getNonEventProp getType cfg)
      let errorProp =
        if t <> typeof<FrameworkElement> then Seq.empty
        else Seq.ofList [Prop.Error { NodeName = "Error" }]
      let events =
        t.GetEvents(BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.DeclaredOnly)
        |> Seq.filter (shouldUseEventProperty cfg)
        |> Seq.choose getEventProp
      Seq.concat [nonEventProps; errorProp; events]
      |> Seq.sort
      |> Seq.toList
  }


let rec removeAbstractLeafTypes (ts: Type array) =
  let filtered = 
    ts |> Array.filter (fun t ->
      not t.IsEffectivelyAbstract
      || ts |> Array.exists (fun tLeaf -> tLeaf.BaseType = t)
    )
  if ts = filtered then filtered else removeAbstractLeafTypes filtered


let getAttachedPropSetters getType cfg (t: Type) =
  let staticSetters = 
    t.GetMethods(BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.DeclaredOnly)
    |> Array.filter (fun mi -> mi.Name.StartsWith "Set")
  let dependencyProps =
    t.GetFields(BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.DeclaredOnly)
    |> Array.filter (fun pi -> 
        pi.Name.EndsWith "Property"
        && pi.FieldType = typeof<DependencyProperty>)
  [|
    for setter in staticSetters do
    for depProp in dependencyProps do
      if setter.Name.Substring(3) = depProp.Name.Substring(0, depProp.Name.Length - 8)
         && setter.GetParameters() |> Array.length = 2
         && shouldUseAttachedProperty getType cfg setter
      then yield setter
  |]


let getGenerationContext (cfg: Config.Root) = 
  let assemblies = cfg.Assemblies |> Array.map Assembly.Load

  let getType fullName =
    let ret =
      assemblies
      |> Seq.tryPick (fun a -> a.GetType fullName |> Option.ofObj)
      |> Option.orElseWith (fun () -> typeof<obj>.Assembly.GetType fullName |> Option.ofObj)
    match ret with
    | Some t -> t
    | None -> failwithf "Unable to find type %s" fullName
  
  let rec getHierarchy (t: Type) =
    match t with
    | null -> ""
    | t -> getHierarchy t.BaseType + "." + t.Name

  let typesToUse =
    assemblies
    |> Array.collect (fun a -> a.GetTypes())
    |> Array.filter (shouldUseType getType cfg)
    |> removeAbstractLeafTypes
    |> Array.sortBy getHierarchy

  { Namespace = cfg.OutputNamespace
    Types = 
      typesToUse 
      |> Array.map (getNodeType getType cfg)
      |> Array.toList
    AttachedProps =
      assemblies
      |> Array.collect (fun a -> a.GetTypes())
      |> Array.filter (shouldUseAttachedPropSourceType cfg)
      |> Array.collect (getAttachedPropSetters getType cfg)
      |> Array.filter (fun mi -> typesToUse |> Array.contains mi.AttachedPropTargetType)
      |> Seq.choose getAttachedProp
      |> Seq.toList
  }
