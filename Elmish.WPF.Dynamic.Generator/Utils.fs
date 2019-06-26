[<AutoOpen>]
module Utils

open System.IO


type TextWriter with
  member this.printf fmt = fprintf this fmt
  member this.printfn fmt = fprintfn this fmt
