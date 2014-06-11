﻿using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using SharpSwift.Converters; 

namespace SharpSwift
{
    class Program
    {
        static string GetIncludesFromTrivia(SyntaxTriviaList triviaList)
        {
            var output = "";
            foreach (var trivia in triviaList)
            {
                if (!trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) &&
                    !trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
                    continue;

                var comment = trivia.ToString().TrimStart('/', '*').Trim();
                if (comment.StartsWith("include"))
                {
                    output += comment + "\r\n";
                }
            }
            return output;
        }

        static string ParseFile(string path, bool doIndent = true)
        {
            Console.WriteLine("Parsing file " + path);

            var output = "//Converted with SharpSwift - https://github.com/matthewsot/SharpSwift\r\n";
            output += "//See https://github.com/matthewsot/DNSwift FMI about these includes\r\n\r\n";
            output += "include DNSwift;\r\n";

            var tree = CSharpSyntaxTree.ParseFile(path);
            var root = (CompilationUnitSyntax)tree.GetRoot();
            var rootNamespace = root.Members.OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
            var classes = rootNamespace.Members.OfType<ClassDeclarationSyntax>();

            foreach (var usingDir in root.Usings)
            {
                //Swift doesn't currently have a good namespace system
                //So for now pretty much everything goes under DNSwift. No other imports neccessary
                /*
                if (usingDir.Name.ToString().StartsWith("System"))
                {
                    output += "include DNSwift." + usingDir.Name + ";\r\n";
                }
                
                else */if(usingDir.GetLeadingTrivia().Any(trivia => trivia.ToString().ToLower().TrimStart('/', '*').StartsWith("universal")))
                {
                    output += "include " + usingDir.Name + ";\r\n";
                }

                output += GetIncludesFromTrivia(usingDir.GetLeadingTrivia());
            }
            output += GetIncludesFromTrivia(root.Usings.Last().GetTrailingTrivia()); //in case they added includes to the bottom
            output += GetIncludesFromTrivia(rootNamespace.GetLeadingTrivia());
            output += "\r\n";

            foreach (var childClass in classes)
            {
                output += ConvertToSwift.SyntaxNode(childClass);
            }

            return doIndent ? Indenter.IndentDocument(output) : output;
        }

        static void Main(string[] args)
        {
            var inPath = args.FirstOrDefault(arg => !arg.StartsWith("-"));
            if(args.Contains("-input"))
            {
                inPath = args[args.ToList().IndexOf("-input") + 1];
            }

            var doIndent = !args.Contains("-noindent");

            var outPath = args.ToList().FirstOrDefault(arg => arg != inPath && !arg.StartsWith("-"));
            if (args.Contains("-output"))
            {
                outPath = args[args.ToList().IndexOf("-output") + 1];
            }

            if (inPath == null)
            {
                Console.WriteLine("You must specify an input file");
                return;
            }

            inPath = inPath.Trim('"');
            outPath = (outPath == null) ? null : outPath.Trim('"');

            if (Directory.Exists(inPath))
            {
                //It's a folder
                foreach (var file in Directory.GetFiles(inPath))
                {
                    if (!file.EndsWith(".cs"))
                        continue;

                    var parsed = ParseFile(file, doIndent);

                    var outputPath = outPath ?? file.Replace(".cs", ".swift");
                    if (!outputPath.EndsWith(".swift"))
                    {
                        outputPath = outputPath.TrimEnd('\\') + "\\" + file.Split('\\').Last().Replace(".cs", ".swift");
                    }

                    using (var writer = new StreamWriter(outputPath))
                    {
                        writer.Write(parsed);
                        writer.Flush();
                    }
                }
            }
            else if (File.Exists(inPath) && inPath.EndsWith(".cs"))
            {
                //It's a file
                var parsed = ParseFile(inPath, doIndent);

                var outputPath = outPath ?? inPath.Replace(".cs", ".swift");

                using (var writer = new StreamWriter(outputPath))
                {
                    writer.Write(parsed);
                    writer.Flush();
                }
            }
            Console.WriteLine("Done.");
            Console.ReadLine();
        }
    }
}
