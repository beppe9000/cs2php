﻿using Lang.Cs.Compiler;
using System;
using Lang.Php.Compiler.Source;

namespace Lang.Php.Compiler.Translator.Node
{
    public class TimeSpanTranslator : IPhpNodeTranslator<CsharpInstancePropertyAccessExpression>
    {
        public IPhpValue TranslateToPhp(IExternalTranslationContext ctx, CsharpInstancePropertyAccessExpression src)
        {
            if (src.Member.DeclaringType == typeof(TimeSpan))
            {
                var to = ctx.TranslateValue(src.TargetObject);
                if (src.Member.Name == "TotalDays") {
                    var aa = new PhpInstanceFieldAccessExpression("days", to, null);
                    return aa;
                }
                throw new NotSupportedException();
            }
            return null;
        }

        public int GetPriority()
        {
            return 1;
        }
    }
}
