module Main

open Domain

[<EntryPoint>]
let main argv =
  let cfg = Config.GetSample()
  Generate.generateAndWrite cfg
  0
