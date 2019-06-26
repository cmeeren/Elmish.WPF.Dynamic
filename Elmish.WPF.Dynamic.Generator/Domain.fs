module Domain

open FSharp.Data

// TODO: Use Newtonsoft.Json and deserialize to specific types
type Config = JsonProvider<"WpfCore.json">


type SingleProp = {

  /// The name of the property on the node type.
  NodeName: string
  
  /// The name of the property on the corresponding real type.
  RealName: string

  /// The type of the property on the node type. Either a simple
  /// node type (e.g. "TextBox") or a namespace-qualified real type
  /// (e.g. "System.Double" or "System.Collections.Generic.IList<System.Double>").
  NodeType: string

  /// The namespace-qualified type of the corresponding real property, e.g.
  /// "System.Double" or "System.Collections.Generic.IList<System.Double>".
  RealType: string

  /// Indicates whether the property type on the node type is known
  /// to be a node type. This value is None if it may or may not be
  /// (e.g. if the property type is obj).
  NodeTypeIsNodeType: bool option

  /// Extra transformations to perform on the node value before setting it to the
  /// view property, e.g. "Option.toNullable" if the node type is Option but the
  /// real type is Nullable. Must include parantheses if more than one function,
  /// e.g. "(f >> g)".
  ExtraMapBeforeSet: string option

  /// The namespace-qualified name of the associated static depenency property,
  /// if it exists, e.g. "System.Windows.Controls.TextBlock.TextProperty"
  DependencyProp: string option
}



type CollectionProp = {
  
  /// The name of the property on the node type.
  NodeName: string
  
  /// The name of the property on the corresponding real type.
  RealName: string
  
  /// The type to use for the node type's collection items. Either a namespace-qualified
  /// real type or a non-qualified node type.
  NodeItemType: string
  
  /// The namespace-qualified type of the real property's collection items, e.g.
  /// "System.Windows.Controls.TextBlock" or "System.Windows.FreezableCollection<ThumbButtonInfo>".
  RealItemType: string

  /// Indicates whether the collection item type on the node type is known to be
  /// a node type. This value is None if it may or may not be (e.g. if the property
  /// type is obj).
  NodeItemTypeIsNodeType: bool option
}


type SelfCollectionProp = {
  
  /// The name of the property on the node type.
  NodeName: string

  /// The type to use for the node type's collection items. Either a namespace-qualified
  /// real type or a non-qualified node type.
  NodeItemType: string
  
  /// The namespace-qualified collection item type, e.g. "System.Double".
  RealItemType: string

  /// Indicates whether the collection item type on the node type is known
  /// to be a node type. This value is None if it may or may not be
  /// (e.g. if the property type is obj).
  NodeItemTypeIsNodeType: bool option
}


type EventProp = {
  
  /// The name of the property on the node type.
  NodeName: string
  
  /// The name of the property on the corresponding real type.
  RealName: string
  
  /// The namespace-qualified EventArgs type, e.g. "System.Windows.Input.MouseEventArgs"
  /// or System.Windows.RoutedPropertyChangedEventArgs<System.Double>.
  EventArgsType: string
}


type ErrorProp = {
  /// The name of the property on the node type.
  NodeName: string
}



/// Represents a property on a node type.
[<RequireQualifiedAccess>]
type Prop =

  /// The property is a single value that may or may not be a node type.
  /// If it is a node type, it should be reconciled recursively.
  | Single of SingleProp
  
  /// The property is a collection of values that may or may not be node types.
  /// The collection itself should be reconciled intelligently, and any node
  /// items should be reconciled recursively.
  | Collection of CollectionProp
  
  /// The property represents the items in a collection-typed node type
  /// where the values may or may not be node types. The collection itself should
  /// be reconciled intelligently, and any node items should be reconciled recursively.
  | SelfCollection of SelfCollectionProp
  
  /// The property is an event.
  | Event of EventProp

  /// Validation error helper. Only used once (hardcoded occurrence).
  | Error of ErrorProp


/// Represents a node type.
type NodeType = {
  
  /// The name of this node type, including any generics, e.g. "TextBlock"
  /// or "FreezableCollection<'T>".
  Name: string
  
  /// The name of this node type, including any generics and constraints,
  /// e.g. "TextBlock" or "FreezableCollection<'T when 'T :> Node>".
  NameWithConstraints: string
  
  /// The node type this type inherits from, including any generics,
  /// e.g. "Panel" or "FreezableCollection<ThumbButtonInfo>".
  BaseNodeType: string
  
  /// Whether the node type is abstract.
  IsAbstract: bool
  
  /// The namespace-qualified type of the corresponding real type that can be used
  /// as a constructor, e.g. "System.Windows.Controls.TextBlock" or
  /// "System.Windows.FreezableCollection<System.Windows.Shell.ThumbButtonInfo>".
  RealType: string
  
  /// The props for this node type.
  Props: Prop list
}


type AttachedProp = {
  
  /// The name of the attached property, e.g. "Dock".
  NodeName: string

  /// The node type corresponding to the type that defines the attached
  /// property, e.g. "DockPanel".
  NodeDeclaringType: string

  /// The node type corresponding to the type that the attached property
  /// targets, e.g. "UIElement".
  NodeTargetType: string

  /// The real type the attached property targets, e.g. "System.Windows.UIElement".
  RealTargetType: string

  /// The type of the attached property value when used in node elements, e.g. "Brush".
  NodeValueType: string

  /// The type of the real attached property value when e.g. "System.Windows.Media.Brush".
  RealValueType: string

  /// The namespace-qualified name of the static setter method, e.g.
  /// "System.Windows.Controls.DockPanel.SetDock"
  StaticSetter: string

  /// Extra transformations to perform on the node value before setting it using
  /// the static setter, e.g. "Option.toNullable" if the node type is Option but the
  /// real type is Nullable. Must include parantheses if more than one function,
  /// e.g. "(f >> g)".
  ExtraMapBeforeSet: string option

  /// The namespace-qualified name of the static depenency property,
  /// e.g. "System.Windows.Controls.DockPanel.DockProperty"
  DependencyProp: string
}


type GenerationContext = {
  
  /// The namespace in which the node types will be placed.
  Namespace: string
  
  /// Info about the node types to generate.
  Types: NodeType list

  /// Info about all attached props.
  AttachedProps: AttachedProp list
}
