using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace partialdownloadgui.Components
{
    public class YoutubePlayerParser
    {
        private string playerFile;
        private string scramblerFunction;
        private string scramblerAlgorithm;
        private List<ScramblerFunction> scramblerFunctions;

        public string PlayerFile { get => playerFile; set => playerFile = value; }
        public string ScramblerFunction { get => scramblerFunction; set => scramblerFunction = value; }
        public List<ScramblerFunction> ScramblerFunctions { get => scramblerFunctions; set => scramblerFunctions = value; }
        public string ScramblerAlgorithm { get => scramblerAlgorithm; set => scramblerAlgorithm = value; }

        public YoutubePlayerParser(string file)
        {
            if (string.IsNullOrEmpty(file)) throw new ArgumentNullException(nameof(file));
            playerFile = file;
            scramblerFunctions = new List<ScramblerFunction>();
        }

        public void FindScramblerFunction()
        {
            string pattern = @"[A-Za-z0-9]+\=function\(([A-Za-z]+)\)\{\1=\1\.split\(\""\""\);.+?return\s\1\.join\(\""\""\)\}";
            Match m = Regex.Match(playerFile, pattern, RegexOptions.Singleline);
            if (m.Success)
            {
                scramblerFunction = Util.RemoveLineBreaks(m.Groups[0].Value);
                Debug.WriteLine(scramblerFunction);
            }
        }

        public void ExtractScramblerFunctionInfo()
        {
            if (string.IsNullOrEmpty(scramblerFunction)) throw new ArgumentNullException(nameof(scramblerFunction));
            string s = Util.RemoveSpaces(scramblerFunction);
            string[] ss = s.Split(';');
            string pattern = @"^([A-Za-z0-9]+)\.([A-Za-z0-9]+)\([A-Za-z0-9]+,([0-9]+)\)$";
            for (int i = 1; i < ss.Length - 1; i++)
            {
                Match m = Regex.Match(ss[i], pattern);
                if (m.Success)
                {
                    ScramblerFunction sf = new ScramblerFunction();
                    sf.FunctionVariable = m.Groups[1].Value;
                    sf.FunctionName = m.Groups[2].Value;
                    sf.Parameter = int.Parse(m.Groups[3].Value);
                    scramblerFunctions.Add(sf);
                }
            }
            Debug.WriteLine(scramblerFunctions.Count);
        }

        public void FindScramblerAlgorithm()
        {
            if (scramblerFunctions.Count == 0) throw new ArgumentOutOfRangeException(nameof(scramblerFunctions), "No scrambler functions present.");
            string pattern = @"var\s" + scramblerFunctions[0].FunctionVariable + @"=\{(.+?)\}\};";
            Match m = Regex.Match(playerFile, pattern, RegexOptions.Singleline);
            if (m.Success)
            {
                scramblerAlgorithm = Util.RemoveLineBreaks(m.Groups[1].Value);
                Debug.WriteLine(scramblerAlgorithm);
            }
        }

        public void MatchAlgorithmWithFunction()
        {
            if (string.IsNullOrEmpty(scramblerAlgorithm)) throw new ArgumentNullException(nameof(scramblerAlgorithm));
            string s = Util.RemoveSpaces(scramblerAlgorithm);
            string[] ss = s.Split("},");
            Dictionary<string, ScramblerType> dic = new();
            foreach (string func in ss)
            {
                int i = func.IndexOf(":");
                if (i == (-1)) continue;
                string name = func.Substring(0, i);
                if (func.Contains("reverse")) dic.Add(name, ScramblerType.Reverse);
                else if (func.Contains("splice")) dic.Add(name, ScramblerType.Slice);
                else dic.Add(name, ScramblerType.Swap);
            }
            foreach (ScramblerFunction f in scramblerFunctions)
            {
                f.Type = dic[f.FunctionName];
                Debug.WriteLine(f.FunctionName + " " + f.Type + " " + f.Parameter);
            }
        }

        public string CalculateSignature(string paramS)
        {
            if (string.IsNullOrEmpty(paramS)) throw new ArgumentNullException(nameof(paramS));
            string signature = paramS;
            foreach (ScramblerFunction f in scramblerFunctions)
            {
                if (f.Type == ScramblerType.Reverse) signature = Reverse(signature);
                else if (f.Type == ScramblerType.Slice) signature = Slice(signature, f.Parameter);
                else signature = Swap(signature, f.Parameter);
            }
            Debug.WriteLine(signature);
            return signature;
        }

        public void Parse()
        {
            FindScramblerFunction();
            ExtractScramblerFunctionInfo();
            FindScramblerAlgorithm();
            MatchAlgorithmWithFunction();
        }

        private static string Reverse(string s)
        {
            char[] cs = s.ToCharArray();
            Array.Reverse(cs);
            return new string(cs);
        }

        private static string Slice(string s, int b)
        {
            return s.Substring(b);
        }

        private static string Swap(string s, int b)
        {
            char[] a = s.ToCharArray();
            char c = a[0];
            a[0] = a[b % a.Length];
            a[b % a.Length] = c;

            return new string(a);
        }
    }
}
