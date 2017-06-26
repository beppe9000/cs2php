﻿using Lang.Php.Mpdf;

namespace E03_ReferencedLibrary
{
    public class Test
    {
        const double BASE_SIZE = 9;
        const string FONT = "dejavusans";
        const double MARGIN = 10;
        const double MARGIN_HEADER = 9;


        public void PhpMain()
        {
            var mpdf = new Mpdf("", "A4",
               BASE_SIZE, FONT,
               MARGIN, MARGIN, MARGIN, MARGIN, MARGIN_HEADER, MARGIN_HEADER, PageOrientation.Portrait);
            mpdf.author = "Mr Compiler";
            mpdf.title = "PDF title";
            mpdf.subject = "PDF subject";

        }
    }
}
