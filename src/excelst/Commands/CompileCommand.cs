using System.CommandLine;
using Excelst.Compiler;

namespace Excelst.Commands;

public static class CompileCommand
{
    public static Command Build()
    {
        var fileArgument = new Argument<FileInfo>("fichier")
        {
            Description = "Fichier source .exl à compiler"
        };

        var command = new Command("compile", "Compile un fichier .exl en fichier .xlsx");
        command.Add(fileArgument);

        command.SetAction((ParseResult result) =>
        {
            var fichier = result.GetValue(fileArgument)!;

            if (!fichier.Exists)
            {
                Console.Error.WriteLine($"Erreur : fichier introuvable : {fichier.FullName}");
                return 1;
            }

            var source = File.ReadAllText(fichier.FullName);
            var outputPath = Path.ChangeExtension(fichier.FullName, ".xlsx");

            try
            {
                var tokens   = new Lexer(source).Tokenize();
                var programme = new Parser(tokens).Parse();
                var workbook  = new ExcelGenerator().Generate(programme);
                workbook.SaveAs(outputPath);

                Console.WriteLine($"Généré : {outputPath}");
                return 0;
            }
            catch (LexerException ex)
            {
                Console.Error.WriteLine($"Erreur de lexer : {ex.Message}");
                return 2;
            }
            catch (ParseException ex)
            {
                Console.Error.WriteLine($"Erreur de syntaxe : {ex.Message}");
                return 3;
            }
            catch (GeneratorException ex)
            {
                Console.Error.WriteLine($"Erreur de génération : {ex.Message}");
                return 4;
            }
        });

        return command;
    }
}
