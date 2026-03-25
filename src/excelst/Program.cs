using System.CommandLine;
using Excelst.Commands;

var rootCommand = new RootCommand("excelst - Compilateur de feuilles Excel");
rootCommand.Add(CompileCommand.Build());

return await rootCommand.Parse(args).InvokeAsync();
