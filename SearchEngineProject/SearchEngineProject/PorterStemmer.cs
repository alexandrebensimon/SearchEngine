﻿using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SearchEngineProject
{
    internal class PorterStemmer
    {
        // A single consonant.
        private const string c = "[^aeiou]";
        // A single vowel.
        private const string v = "[aeiouy]";

        // A sequence of consonants; the second/third/etc consonant cannot be 'y'.
        private const string C = c + "[^aeiouy]*";
        // A sequence of vowels; the second/third/etc cannot be 'y'.
        private const string V = v + "[aeiou]*";

        // This regex tests if the token has measure > 0 [at least one VC].
        private static readonly Regex MGr0 = new Regex("^(" + C + ")?" + V + C);

        // Add more Regex variables for the following patterns.
        // M equals 1: token has measure == 1.
        private static readonly Regex MEq1 = new Regex("^(" + C + ")?" + V + C + "(" + V + ")?$");

        // M greater than 1: token has measure > 1.
        private static readonly Regex MGr1 = new Regex("^(" + C + ")?" + V + C + V + C);

        // Vowel: token has a vowel after the first (optional) C.
        private static readonly Regex Vowel = new Regex("^(" + C + ")?" + V);

        // Double consonant: token ends in two consonants that are the same, 
        //			unless they are L, S, or Z. (look up "backreferencing" to help 
        //			with this).
        private static readonly Regex Dbcons = new Regex(@"([^aeioulszy])\1$");

        // M equals 1, Cvc: token is in Cvc form, where the last c is not w, x, 
        //			or y.
        private static readonly Regex MEq1Cvc = new Regex("^(" + C + ")" + v + "[^aeiouwxy]$");

        private static readonly Dictionary<string, string> SuffixListS2 = new Dictionary<string, string>
        {
            {"ational", "ate"},
            {"tional", "tion"},
            {"enci", "ence"},
            {"anci", "ance"},
            {"izer", "ize"},
            {"bli", "ble"},
            {"logi", "log"},
            {"alli", "al"},
            {"entli", "ent"},
            {"eli", "e"},
            {"ousli", "ous"},
            {"ization", "ize"},
            {"ation", "ate"},
            {"ator", "ate"},
            {"alism", "al"},
            {"iveness", "ive"},
            {"fulness", "ful"},
            {"ousness", "ous"},
            {"aliti", "al"},
            {"iviti", "ive"},
            {"biliti", "ble"}
        };

        private static readonly Dictionary<string, string> SuffixListS3 = new Dictionary<string, string>
        {
            {"icate", "ic"},
            {"ative", ""},
            {"alize", "al"},
            {"iciti", "ic"},
            {"ical", "ic"},
            {"ful", ""},
            {"ness", ""}
        };

        private static readonly string[] SuffixListS4 = {
            "al",
            "ance",
            "ence",
            "er",
            "ic",
            "able",
            "ible",
            "ant",
            "ement",
            "ment",
            "ent",
            "ion",
            "ou",
            "ism",
            "ate",
            "iti",
            "ous",
            "ive",
            "ize"
        };

        public static string ProcessToken(string token)
        {
            // Token must be at least 3 chars.
            if (token.Length < 3) return token;

            // Step 1a.
            if (token.EndsWith("sses") || token.EndsWith("ies"))
                token = token.Substring(0, token.Length - 2);
            // Program the other steps in 1a. 
            // Note that Step 1a.3 implies that there is only a single 's' as the 
            //  suffix; ss does not count. you may need a regex here for 
            //  "not s followed by s".
            else if (!token.EndsWith("ss") && token.EndsWith("s"))
                token = token.Substring(0, token.Length - 1);


            // Step 1b.
            bool doStep1Bb = false;
            if (token.EndsWith("eed"))
            {
                // 1b.1.
                // Token.Substring(0, token.Length - 3) is the stem prior to "eed".
                // If that has m>0, then remove the "d".
                string stem = token.Substring(0, token.Length - 3);
                if (MGr0.IsMatch(stem))
                {
                    token = stem + "ee";
                }
            }
            // Program the rest of 1b. set the bool doStep1bb to true if Step 1b* 
            //  should be performed.
            else if (token.EndsWith("ed"))
            {
                string stem = token.Substring(0, token.Length - 2);
                if (Vowel.IsMatch(stem))
                {
                    token = stem;
                    doStep1Bb = true;
                }
            }
            else if (token.EndsWith("ing"))
            {
                string stem = token.Substring(0, token.Length - 3);
                if (Vowel.IsMatch(stem))
                {
                    token = stem;
                    doStep1Bb = true;
                }
            }

            // Step 1b*, only if the 1b.2 or 1b.3 were performed.
            if (doStep1Bb)
            {
                if (token.EndsWith("at") || token.EndsWith("bl") || token.EndsWith("iz"))
                {
                    token = token + "e";
                }
                else if (Dbcons.IsMatch(token))
                {
                    token = token.Substring(0, token.Length - 1);
                }
                else if (MEq1Cvc.IsMatch(token))
                {
                    token = token + "e";
                }
                // Use the regexes you wrote for 1b*.4 and 1b*.5.
            }

            // Step 1c.
            // Program this step. test the suffix of 'y' first, then test the 
            //  condition *v*.
            if (token.EndsWith("y"))
            {
                string stem = token.Substring(0, token.Length - 1);
                if (Vowel.IsMatch(stem))
                {
                    token = stem + "i";
                }
            }


            // Step 2.
            // Program this step. for each suffix, see if the token ends in the 
            //  suffix. 
            //		* if it does, extract the stem, and do NOT test any other suffix.
            //    * take the stem and make sure it has m > 0.
            //			* if it does, complete the step. if it does not, do not 
            //				attempt any other suffix.


            // You may want to write a helper method for this. a matrix of 
            //  "suffix"/"replacement" pairs might be helpful. It could look like
            //  string[][] step2pairs = {  new string[] {"ational", "ate"}, 
            //										new string[] {"tional", "tion"}, ....
            token = Step23(token, SuffixListS2);

            // Step 3.
            // Program this step. the rules are identical to step 2 and you can use
            //  the same helper method. you may also want a matrix here.
            token = Step23(token, SuffixListS3);


            // Step 4.
            // Program this step similar to step 2/3, except now the stem must have
            //  measure > 1.
            // Note that ION should only be removed if the suffix is SION or TION, 
            //  which would leave the S or T.
            // As before, if one suffix matches, do not try any others even if the 
            //  stem does not have measure > 1.
            foreach (string suffix in SuffixListS4)
            {
                if (token.EndsWith(suffix))
                {
                    string stem = token.Substring(0, token.Length - suffix.Length);
                    if (MGr1.IsMatch(stem))
                    {
                        if (suffix != "ion" || stem.EndsWith("s") || stem.EndsWith("t"))
                        {
                            token = stem;
                        }
                    }
                    break;
                }
            }

            // Step 5.
            // Program this step. you have a regex for m=1 and for "Cvc", which
            //  you can use to see if m=1 and NOT Cvc.
            if (token.EndsWith("e"))
            {
                string stem = token.Substring(0, token.Length - 1);
                if (MGr1.IsMatch(stem) || (MEq1.IsMatch(stem) && !MEq1Cvc.IsMatch(stem)))
                {
                    token = stem;
                }
            }

            if (token.EndsWith("ll") && MGr1.IsMatch(token))
            {
                token = token.Substring(0, token.Length - 1);
            }


            // All your code should change the variable token, which represents
            //  the stemmed term for the token.
            return token;
        }

        private static string Step23(string token, Dictionary<string, string> suffixList)
        {
            foreach (var suffix in suffixList.Keys)
            {
                if (!token.EndsWith(suffix)) continue;
                var stem = token.Substring(0, token.Length - suffix.Length);
                if (MGr0.IsMatch(stem))
                {
                    return stem + suffixList[suffix];
                }
                return token;
            }
            return token;
        }
    }
}