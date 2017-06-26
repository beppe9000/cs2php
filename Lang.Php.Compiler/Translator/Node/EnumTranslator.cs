﻿using Lang.Cs.Compiler;
using System;

namespace Lang.Php.Compiler.Translator.Node
{
    public class EnumTranslator : IPhpNodeTranslator<CsharpMethodCallExpression>
    {
        public IPhpValue TranslateToPhp(IExternalTranslationContext ctx, CsharpMethodCallExpression src)
        {
            if (src.MethodInfo.DeclaringType != typeof(Enum))
                return null;
            var fn = src.MethodInfo.ToString();
            if (fn == "System.Object Parse(System.Type, System.String)")
            {
                var a = src.Arguments[1].MyValue;
                var b = ctx.TranslateValue(a);
                return b;
            }
            throw new NotImplementedException();
        }

        public int GetPriority()
        {
            return 1;
        }
    }
}
