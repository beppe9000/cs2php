﻿using Lang.Cs.Compiler;
using System;
using Lang.Php.Compiler.Source;

namespace Lang.Php.Compiler.Translator.Node
{
    public class DateTimeTranslator : IPhpNodeTranslator<CallConstructor>,
                IPhpNodeTranslator<ClassPropertyAccessExpression>,
                IPhpNodeTranslator<CsharpMethodCallExpression>,
                IPhpNodeTranslator<BinaryOperatorExpression>,
                IPhpNodeTranslator<CsharpInstancePropertyAccessExpression>
    {
        #region Methods

        // Public Methods 

        public int GetPriority()
        {
            return 1;
        }

        public IPhpValue TranslateToPhp(IExternalTranslationContext ctx, CallConstructor src)
        {
            if (src.Info.DeclaringType != typeof(DateTime))
                return null;
            switch (src.Info.ToString())
            {
                case "Void .ctor(Int32, Int32, Int32)":
                    {
                        //                $date = new DateTime();
                        //$date->setDate(2001, 2, 3);
                        var dtObject = PhpMethodCallExpression.MakeConstructor("DateTime", null);
                        dtObject.DontIncludeClass = true;
                        // date_date_set 
                        var b = new PhpMethodCallExpression("date_date_set",
                            dtObject,
                            ctx.TranslateValue(src.Arguments[0]),
                            ctx.TranslateValue(src.Arguments[1]),
                            ctx.TranslateValue(src.Arguments[2])
                            );
                        var c = new PhpMethodCallExpression("date_time_set",
                            b,
                            new PhpConstValue(0),
                            new PhpConstValue(0),
                            new PhpConstValue(0)
                            );
                        var mktime = new PhpMethodCallExpression("mktime",
                            new PhpConstValue(0),
                            new PhpConstValue(0),
                            new PhpConstValue(0),
                            ctx.TranslateValue(src.Arguments[1]), //month
                            ctx.TranslateValue(src.Arguments[2]),// day
                            ctx.TranslateValue(src.Arguments[0]) // year
                            );
                        var epoch = new PhpBinaryOperatorExpression(".", new PhpConstValue("@"), mktime);
                        dtObject.Arguments.Add(new PhpMethodInvokeValue(epoch));
                        return dtObject;
                        // int mktime ([ int $hour = date("H") [, int $minute = date("i") [, int $second = date("s") [, int $month = date("n") [, int $day = date("j") [, int $year = date("Y") [, int $is_dst = -1 ]]]]]]] )
                        // $datetimeobject = new DateTime(mktime(0, 0, 0, $data[$j]['month'], $data[$j]['day'],$data[$j]['year']));
                    }
            }
            throw new NotImplementedException();
        }

        public IPhpValue TranslateToPhp(IExternalTranslationContext ctx, ClassPropertyAccessExpression src)
        {
            if (src.Member.DeclaringType != typeof(DateTime))
                return null;
            switch (src.Member.Name)
            {
                case "Now":
                    {
                        // $date = new DateTime('2000-01-01');
                        var c = PhpMethodCallExpression.MakeConstructor("DateTime", null);
                        c.DontIncludeClass = true;
                        return c;
                    }
            }

            throw new NotImplementedException();
        }

        public IPhpValue TranslateToPhp(IExternalTranslationContext ctx, CsharpMethodCallExpression src)
        {
            if (src.MethodInfo.DeclaringType != typeof(DateTime))
                return null;
            var fn = src.MethodInfo.ToString();
            if (src.MethodInfo.Name == "ToString")
            {
                var to = ctx.TranslateValue(src.TargetObject);
                var c = new PhpMethodCallExpression(to, "format", new PhpConstValue("Y-m-d H:i:s"));
                return c;
            }
            if (fn == "System.TimeSpan op_Subtraction(System.DateTime, System.DateTime)")
            {
                var l = ctx.TranslateValue(src.Arguments[0].MyValue);
                var r = ctx.TranslateValue(src.Arguments[1].MyValue);
                var c = new PhpMethodCallExpression("date_diff", r, l);
                return c;
            }
            if (fn == "Boolean op_GreaterThanOrEqual(System.DateTime, System.DateTime)")
                return new PhpBinaryOperatorExpression(">=",
                    ctx.TranslateValue(src.Arguments[0]),
                    ctx.TranslateValue(src.Arguments[1]));
            if (fn == "Boolean op_Inequality(System.DateTime, System.DateTime)")
                return new PhpBinaryOperatorExpression("!=",
                      ctx.TranslateValue(src.Arguments[0]),
                      ctx.TranslateValue(src.Arguments[1]));
            if (fn == "Boolean op_Equality(System.DateTime, System.DateTime)")
                return new PhpBinaryOperatorExpression("==",
                      ctx.TranslateValue(src.Arguments[0]),
                      ctx.TranslateValue(src.Arguments[1]));

            if (fn == "System.DateTime AddDays(Double)")
                return TranslateAddInterval(ctx, src, SecPerDay);
            if (fn == "System.DateTime AddHours(Double)")
                return TranslateAddInterval(ctx, src, SecPerHour);

            throw new NotImplementedException(fn);
        }

        public IPhpValue TranslateToPhp(IExternalTranslationContext ctx, BinaryOperatorExpression src)
        {
            if (src.OperatorMethod == null) return null;
            return null;
        }

        public IPhpValue TranslateToPhp(IExternalTranslationContext ctx, CsharpInstancePropertyAccessExpression src)
        {
            if (src.Member.DeclaringType != typeof(DateTime))
                return null;

            var to = ctx.TranslateValue(src.TargetObject);
            if (src.Member.Name == "Date")
            {
                if (to.ToString() == "new DateTime()")
                {
                    // echo 'Current time: ' . date('Y-m-d H:i:s') . "\n";
                    var co = new PhpMethodCallExpression("date", new PhpConstValue(DateOnly));
                    var co1 = new PhpMethodCallExpression("date_create_from_format", new PhpConstValue(DateOnly), co);
                    return co1;
                }
                else
                {
                    var _for = new PhpMethodCallExpression(to, "format", new PhpConstValue(DateOnly));
                    var co = new PhpMethodCallExpression("date_create_from_format",
                        _for, new PhpConstValue(DateOnly));
                    return co;
                }
            }
            if (src.Member.Name == "Day")
                return GetDatePart(to, "d");
            if (src.Member.Name == "Month")
                return GetDatePart(to, "m");
            if (src.Member.Name == "Year")
                return GetDatePart(to, "Y");

            if (src.Member.Name == "Hour")
                return GetDatePart(to, "H");
            if (src.Member.Name == "Minute")
                return GetDatePart(to, "i");
            if (src.Member.Name == "Second")
                return GetDatePart(to, "s");

            throw new NotImplementedException();
        }
        // Private Methods 

        private static IPhpValue GetDatePart(IPhpValue to, string phpDatePart)
        {
            if (to.ToString() == "new DateTime()")
            {
                throw new NotImplementedException();
                // echo 'Current time: ' . date('Y-m-d H:i:s') . "\n";
                //                var co = new PhpMethodCallExpression("date", new PhpConstValue(DATE_ONLY));
                //                var co1 = new PhpMethodCallExpression("date_create_from_format ", new PhpConstValue(DATE_ONLY), co);
                //                return co1;
            }
            else
            {
                var _for = new PhpMethodCallExpression(to, "format", new PhpConstValue(phpDatePart));
                return MakeInt(_for);
            }
        }

        static IPhpValue GetIntervalString(IExternalTranslationContext ctx, IValue v, int mnoznik)
        {
            if (v is FunctionArgument)
                v = (v as FunctionArgument).MyValue;

            if (v is ConstValue)
            {
                var vv = (v as ConstValue).MyValue;
                IPhpValue iphp;
                if (vv is double)
                {
                    var phpString = Mk((double)vv * mnoznik);
                    IValue cs = new ConstValue(phpString);
                    iphp = ctx.TranslateValue(cs);
                    return iphp;
                }
                if (vv is int)
                {
                    var phpString = Mk((int)vv * mnoznik);
                    IValue cs = new ConstValue(phpString);
                    iphp = ctx.TranslateValue(cs);
                    return iphp;
                }
                throw new NotSupportedException();
            }
            throw new NotSupportedException();
        }

        static IPhpValue MakeInt(IPhpValue x)
        {
            return new PhpMethodCallExpression("intval", x);
        }

        static string Mk(double sec)
        {
            var sec1 = (int)Math.Round(sec);
            if (sec1 % SecPerDay == 0)
                return string.Format("P{0}D", sec1 / SecPerDay);
            if (sec1 % SecPerHour == 0)
                return string.Format("PT{0}H", sec1 / SecPerHour);
            if (sec1 % 60 == 0)
                return string.Format("PT{0}M", sec1 / SecPerMin);
            return string.Format("PT{0}S", sec1);
        }

        private static IPhpValue TranslateAddInterval(IExternalTranslationContext ctx, CsharpMethodCallExpression src, int mnoznik)
        {
            var intervalString = GetIntervalString(ctx, src.Arguments[0], mnoznik);
            var interval = PhpMethodCallExpression.MakeConstructor("DateInterval", null, intervalString);
            interval.DontIncludeClass = true;
            var targetObject = ctx.TranslateValue(src.TargetObject);
            var methd = new PhpMethodCallExpression("date_add", null, targetObject, interval);
            return methd;
        }

        #endregion Methods

        #region Fields

        const string DateOnly = "Y-m-d";
        const int SecPerDay = 24 * 3600;
        const int SecPerHour = 3600;
        const int SecPerMin = 60;

        #endregion Fields
    }
}
