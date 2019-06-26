Elmish.WPF.Dynamic
==================

**A dynamic Elmish UI library for WPF**

See also [Elmish.WPF](https://github.com/elmish/Elmish.WPF) for a robust “half-elmish” solution with static XAML views.

What is this?
-------------

Elmish.WPF.Dynamic is a library that allows you to write dynamic WPF views using an Elmish architecture.

It is superficially similar to [Fabulous](https://github.com/fsprojects/Fabulous) in using a generator and config file. However, the `view` syntax is different, and under the hood, the “shadow DOM” is more strongly typed in Elmish.WPF.Dynamic.

How is it used?
---------------

Check out the `TestApp` project in this repo for a complete working app. Some familiarity with Elmish is assumed.

The `view` syntax is based on mutable properties. Note that this is just a syntax for creating shadow DOM elements; it has no impact on the immutability of your model.

For example, using normal property initialization syntax:

```f#
TextBlock(
  Text = model.MyText,
  IsHyphenationEnabled = true
)
```

There are also constructor overloads taking an initializer function. This provides better discoverability since you can “dot into” the types to discover the available properties. It also allows you to factor out common setters.

```f#
TextBlock(fun tb ->
  setCommonTextBlockProps tb
  tb.Text <- model.MyText
  tb.IsHyphenationEnabled <- true
)
```

You can even mix and match:

```f#
TextBlock((fun tb ->
  setCommonTextBlockProps tb
  tb.Text <- model.MyText),
  IsHyphenationEnabled = true
)
```

Or:

```f#
TextBlock(
  setCommonTextBlockProps,
  Text = model.MyText,
  IsHyphenationEnabled = true
)
```

The `view` function must either return a single `Window` (and the program started with `Program.runWindow`) or a list of `Window`s (and the program started with `Program.runWindows`).

### Available types and props

As a general rule, every relevant WPF `DependencyObject`  has a corresponding shadow DOM type (ultimately deriving from a based type currently called `Node`, like Fabulous’ `ViewElement`), and all relevant properties on each control have a corresponding property on the shadow DOM type. The shadow DOM type hierarchy mirrors the hierarchy of the WPF controls.

Attached props are defined as extension properties on the relevant types. For example, `DockPanel.Dock` is defined as an extension property `DockPanel_Dock` on `UIElement`.

In addition, the following special props exist:

* `Key` on all elements – used for reconciling collections intelligently, [similar to React](https://reactjs.org/docs/reconciliation.html)
* `Ref` on all elements – used with `ViewRef<_>` to get access to the raw WPF controls/views
* `Error` on `FrameworkElement` – used to set a validation error for the element
* `InitialView` for setting the initial raw WPF view, useful for interoping with 3rd party resources (which may e.g. be obtained using `getResource`, see the sample app)

Current project status
--------------

This is a fairly usable proof of concept. If the current limitations are not important to you, I see nothing wrong with creating apps with it. (There may of course be bugs, as always.)

Some important and helpful batteries are included, such as `lazyWith` for memoizing views, `elmEq` and `refEq` as sane defaults for `lazyWith`, `getResource` to interop with application resources, and a `TextChangedEventArgs.Text` extension property to easily retrieve the current text of a `TextBox` when changed. The generator should also work for 3rd party controls and attached props, though that is currently untested. However, there are at least a few limitations which may or may not be significant for you. They are described below.

Wanted: Someone to carry the project forward
--------------------------------------------

I have no immediate plans to continue working on this, and I can’t promise I’ll get back to it in the future. **Anyone is welcome to drive the project forward, whether by taking over this repo, creating a fork, creating your own project inspired by this, or submitting PRs.** If you are interested in doing significant work, please take over the project instead of submitting PRs.

For me, this project is purely based on personal interest, and at the moment I’m more interested in creating apps with [Fable.React and Electron](https://github.com/cmeeren/fable-elmish-electron-material-ui-demo) due to the battle-tested nature of React and Electron and the synergy of React with an Elmish architecture. I am also thinking that perhaps static XAML views with [Elmish.WPF](https://github.com/elmish/Elmish.WPF) synergizes better with WPF than dynamic views, since WPF is heavily based on bindings, templates, etc. While static views are far from as composable as dynamic views, [Elmish.WPF](https://github.com/elmish/Elmish.WPF) puts all the power of WPF at your fingertips, while still allowing most of the goodness of an Elmish architecture.

Current limitations
-----------------------------

In short, it seems that some WPF functionality that is desirable also in an Elmish architecture, such as virtualization and key bindings, depend on functionality that should generally *not* be available in an Elmish architecture, such as templates/bindings and `ICommand`s. While solutions can be found, it’s certainly not trivial.

The most notable current limitations are:

* Templates and bindings are excluded from the generated code, which means that the following that depend on them are not available:
  * Virtualization
  * `DataGrid`
  * ` ListView`
* Everything related to commands is excluded, which includes `KeyBinding` and `MouseBinding`. This may make it harder to create keyboard shortcuts etc.

TODOs
-----

### Primary challenges

* Get virtualization to work. For inspiration, see https://github.com/fsprojects/Fabulous/issues/455. Must probably change from `ItemsControl.Items` to `ItemsControl.ItemsSource`, use `ObservableCollection` and templates, and create some helper types.
* Determine if some functionality is only available with commands, and if so, add them and/or create wrappers/helpers to allow idiomatic Elmish usage

### Other challenges

* Determine whether `*Selector` props are needed (e.g. `ComboBox.GroupStyleSelector`)
* Determine what to do with types that implement [`ISupportInitialize`](https://docs.microsoft.com/en-us/dotnet/api/system.componentmodel.isupportinitialize?view=netframework-4.8) (and similar patterns, such as the `BeginInit` and `EndInit` methods of [`BitmapSource`](https://docs.microsoft.com/en-us/dotnet/api/system.windows.media.imaging.bitmapsource?view=netframework-4.8))
* Determine what to do with properties that reference other views. Are they necessary, and if so, how should they be used in an Elmish architecture?
  * `Label.Target`
  * `Tooltip.PlacementTarget`
  * `ContextMenu.PlacementTarget`
  * `ContextMenu.CustomPopupPlacementCallback`
  * `StackPanel.ScrollOwner`
  * `VirtualizingStackPanel.ScrollOwner`
  * `Popup.PlacementTarget`
  * `Popup.CustomPopupPlacementCallback`
  * `ContextMenuService_PlacementTarget`
  * `FocusManager_FocusedElement`
  * `Storyboard_Target`
* Animation – currently untested. Does it work as-is, or should usage be improved? How?
  * `Storyboard`
  * `Storyboard_TargetProperty`
  * `VisualState` and related stuff
* Test `FreezableCollection<'T>` (e.g. `ThumbButtonInfoCollection`) since it’s the only  generic shadow DOM type

### Missing props/types/features

* `ContainerVisual.Children` is of type `VisualCollection`, which doesn't implement `IList` or another modifiable collection interface, so it isn't picked up by the generator. Must be used as-is (as `VisualCollection`). Find a way to get the collection update helper to deal with this. (That is, if `ContainerVisual` and the deriving `DrawingVisual` is at all necessary to include.)
* Currently, abstract real types become abstract generated shadow DOM types (by design). However, the “abstract” shadow DOM types should probably be instantiable in order to make it easier to use `InitialView`. For example, if setting a `Brush` resource (`Brush` is abstract) using `InitialView`, one must currently know whether the resource is a `SolidColorBrush` etc. and instantiate the correct shadow DOM type. It would be great to be able to simply instantiate a shadow DOM `Brush` and pass it the real `Brush`. To accomplish this, abstract real types can probably be modelled as non-`[<Abstract>]` shadow DOM types with only one constructor taking the `InitialElement`.
* Some “official” WPF controls are in assemblies that are not yet included in the generator config. Include them and verify all generated types and props:
  * [`Ribbon`](https://docs.microsoft.com/en-us/dotnet/api/system.windows.controls.ribbon.ribbon) and related stuff in `System.Windows.Controls.Ribbon.dll`

### 3rd party shadow DOM generation

* Currently untested. Test with e.g. MaterialDesignInXamlToolkit, which has both custom controls and attached props (and is already used in the sample app for styling).

### General improvements

* Check out Fabulous’ `fix` function – it it useful here?

### Cleanup of generated code

* Exclude all `Sibling*` properties if they’re not needed:
  * `Block.SiblingBlocks`
  * `Inline.SiblingInlines`
  * `ListItem.SiblingListItems`
* Exclude all `*StringFormat` properties if they’re not needed:
  * `ContentControl.ContentStringFormat`
  * `ItemsControl.ItemStringFormat`

### Optimizations

* Should there be separate update helpers depending on whether we know the element to be updated to be a shadow DOM element or not? Would avoid some boxing and type checking, but unsure if it’s worth it.

### Config file format

* Don’t use a type provider – define the schema as types instead, document the properties, and use e.g. Newtonsoft.Json.
* Support regex matching on exclusions for convenience?
* Use `globallyIgnoredTypes` only for excluding shadow DOM types? Currently properties with matching types are also excluded. Separating these concerns is necessary if we need to exclude a shadow DOM type while still generating a shadow DOM property that has that type.

Implementation details
----------------------

This section is mostly relevant for anyone wanting to take over.

Note that due to the WIP/POC nature of this project, any file/module/type/variable names may or may not be well thought through or accurately reflect the current state of the items they describe. Most names should be OK, but please rename to your liking.

The solution contains three projects:

* `Elmish.WPF.Dynamic` is what would be published to NuGet.  It contains all helpers and all generated WPF controls.
* `Elmish.WPF.Dynamic.Generator` is a console app used to generate the code. It contains a fairly well-documented domain model. Currently, it only uses the `WpfCore.json` config file when run, but supporting command-line arguments is just a tiny change in `Main.fs`.
* `Elmish.WPF.TestApp` is an example app which has served testing purposes while developing and may or may not make much sense or look very pretty.

Some notes about the generated shadow DOM types:

* All shadow DOM types derive from `Node` in `Helpers.fs`
* `Node` itself mostly handles logic for validation error and attached props
* The generated types are checked in so that it’s easy to inspect any changes

Please ask if you have more questions about the code.

