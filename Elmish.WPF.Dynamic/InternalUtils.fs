[<AutoOpen>]
module internal Elmish.WPF.Dynamic.InternalUtils


open System
open System.Linq.Expressions
open System.Reflection
open System.Collections.Generic

/// Returns a fast, untyped getter for the property specified by the PropertyInfo.
/// The getter takes an instance and returns a property value.
let buildUntypedGetter (propertyInfo: PropertyInfo) : obj -> obj =
  let method = propertyInfo.GetMethod
  let objExpr = Expression.Parameter(typeof<obj>, "o")
  let expr =
    Expression.Lambda<Func<obj, obj>>(
      Expression.Convert(
        Expression.Call(
          Expression.Convert(objExpr, method.DeclaringType), method),
          typeof<obj>),
      objExpr)
  let action = expr.Compile()
  fun target -> action.Invoke(target)


type ElmEq<'a>() =

  static let gettersAndEq =
    typeof<'a>.GetProperties()
    |> Array.map (fun pi ->
        let getter = buildUntypedGetter pi
        let eq =
          if pi.PropertyType.IsValueType || pi.PropertyType = typeof<string>
          then (fun (a, b) -> a = b)
          else obj.ReferenceEquals
        getter, eq
    )

  static member Eq x1 x2 =
    gettersAndEq |> Array.forall (fun (get, eq) -> eq (get (box x1), get (box x2)))


type Memoizations<'key, 'inp, 'res when 'key : equality>() =
  static let memoized = System.Collections.Generic.Dictionary<'key, 'inp * 'res>()
  static member Memoized = memoized


type ErrorHelper() =

  let errorsChanged = DelegateEvent<EventHandler<ComponentModel.DataErrorsChangedEventArgs>>()
  let mutable error : string option = None

  member this.Dummy = ""

  member this.Error
    with set x = 
      if x <> error then
        error <- x
        errorsChanged.Trigger([| box this; box <| ComponentModel.DataErrorsChangedEventArgs "Dummy" |])

  interface ComponentModel.INotifyDataErrorInfo with
    member this.GetErrors s =
      match error with
      | None -> upcast []
      | Some err -> upcast [err]
    member this.HasErrors = error.IsSome
    [<CLIEvent>]
    member this.ErrorsChanged = errorsChanged.Publish


type Dictionary<'key, 'value> with
  
  member this.TryFind key =
    match this.TryGetValue key with
    | true, x -> ValueSome x
    | false, _ -> ValueNone
