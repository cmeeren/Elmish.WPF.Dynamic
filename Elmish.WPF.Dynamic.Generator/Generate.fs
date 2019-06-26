module Generate

open System.IO
open Domain
open Analyze

let generateAndWrite (cfg: Config.Root) =
  let generationCtx = getGenerationContext cfg
  use w = new StringWriter()
  Write.everything w generationCtx 
  File.WriteAllText(cfg.OutputPath, w.ToString())
