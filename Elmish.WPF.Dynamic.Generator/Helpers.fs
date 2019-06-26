[<AutoOpen>]
module Helpers

open System
open System.Collections.Generic
open System.Reflection
open System.Windows.Markup
open Domain



type Type with

  member this.IsAssignableTo<'T>() =
    typeof<'T>.IsAssignableFrom this

  member this.IsAssignableTo(t: Type) =
    t.IsAssignableFrom this

  member this.IsObsolete =
    this.GetCustomAttributes(typeof<ObsoleteAttribute>, false) |> Array.isEmpty |> not

  member this.HasDefaultConstructor =
    this.GetConstructor(BindingFlags.Public ||| BindingFlags.Instance, null, [||], [||]) |> isNull |> not

  member this.IsEffectivelyAbstract =
    this.IsAbstract || not this.HasDefaultConstructor

  /// Returns the entire inheritance hierarchy of the type, with the immediate
  /// base type first and the most distant base type last.
  member this.InheritanceHierarchy =
    if isNull this.BaseType then []
    else
      this.BaseType :: this.BaseType.InheritanceHierarchy


type PropertyInfo with

  member this.IsObsolete =
    this.GetCustomAttribute<ObsoleteAttribute>() |> isNull |> not

  member this.IsSettable =
    this.GetSetMethod() |> isNull |> not

  member this.IsContentProp =
    match this.DeclaringType.GetCustomAttribute<ContentPropertyAttribute>() with
    | null -> false
    | attr -> attr.Name = this.Name


type EventInfo with

  member this.IsObsolete =
    this.GetCustomAttribute<ObsoleteAttribute>() |> isNull |> not


type MethodInfo with

  member this.AttachedPropTargetType =
    this.GetParameters().[0].ParameterType

  member this.AttachedPropValueType =
    this.GetParameters().[1].ParameterType

  member this.IsObsolete =
    this.GetCustomAttribute<ObsoleteAttribute>() |> isNull |> not


type Config.Root with
    
  member this.GetCollectionItemType (getType: string -> Type) (collectionType: Type) =
    this.CollectionItemTypeMapping
    |> Array.tryPick (fun x -> 
        if collectionType.IsAssignableTo (getType x.CollectionType)
        then getType x.ItemType |> Some
        else None)



let getCollectionItemType getType (cfg: Config.Root) (collectionType: Type) =
  let itemTypeFromConfig = cfg.GetCollectionItemType getType collectionType
  let itemTypeFromIEnumerable =
    collectionType.GetInterfaces()
    |> Array.tryPick (fun t -> 
        if t.IsGenericType
           && t.GetGenericTypeDefinition() = typedefof<IEnumerable<_>>
        then t.GetGenericArguments().[0] |> Some
        else None
    )
  match itemTypeFromConfig, itemTypeFromIEnumerable with
  | None, None -> None
  | None, Some t -> Some t
  | Some t, None -> Some t
  | Some tConf, Some tGeneric ->
      if tConf = tGeneric then 
        printfn "WARNING: Was able to infer item type %s for collection type %s from generic interface. The configured value can be removed." tGeneric.FullName collectionType.FullName 
        Some tGeneric
      else
        printfn "WARNING: Collection type %s has item type %s from generic interface but item type %s from config. Using config value." collectionType.FullName tGeneric.FullName tConf.FullName
        Some tConf


type Config.Root with
    
  member this.IsPropertyIgnored (mi: MemberInfo) =
    this.IgnoredProperties |> Array.contains mi.Name
    || this.IgnoredProperties |> Array.contains (sprintf "%s.%s" mi.DeclaringType.FullName mi.Name)

  member this.IsTypeGloballyIgnored (t: Type) =
    this.GloballyIgnoredTypes |> Array.contains t.FullName

  member this.IsPropertyTypeGloballyIgnored (getType: string -> Type) (t: Type) =
    this.GloballyIgnoredPropertyTypes
    |> Array.exists (fun ignored ->
        t.FullName = ignored
        || t.IsAssignableTo(getType ignored))
    ||
    this.GloballyIgnoredTypes
    |> Array.exists (fun ignored ->
        t.FullName = ignored
        || t.IsAssignableTo(getType ignored)
        || getCollectionItemType getType this t |> Option.map (fun t -> t.Name) = Some ignored
        || getCollectionItemType getType this t |> Option.map (fun t -> t.IsAssignableTo(getType ignored)) = Some true
    )

  member this.GetPropertyCollectionItemType getType (pi: PropertyInfo) =
    let typeFromCollection = getCollectionItemType getType this pi.PropertyType
    let typeFromPropertyCfg = 
      this.PropertyItemTypeMapping 
      |> Array.tryPick(fun x -> 
          if x.Property = sprintf "%s.%s" pi.DeclaringType.FullName pi.Name 
          then x.ItemType |> getType |> Some 
          else None)
    match typeFromCollection, typeFromPropertyCfg with
    | None, None -> None
    | Some t, None -> Some t
    | None, Some t -> Some t
    | Some tColl, Some tProp when tColl = tProp ->
        printfn "WARNING: Collection property %s.%s with type %s has matching configured item type both globally and property-specific; consider dropping the property-specific configuration" pi.DeclaringType.FullName pi.Name pi.PropertyType.FullName
        Some tProp
    | Some tColl, Some tProp ->
        printfn "WARNING: Collection property %s.%s with type %s has global configured item type %s but property-specific configured item type %s. Using property-specific value." pi.DeclaringType.FullName pi.Name pi.PropertyType.FullName tColl.FullName tProp.FullName
        Some tProp