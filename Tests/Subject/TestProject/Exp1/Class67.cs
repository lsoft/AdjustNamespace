#pragma warning disable IDE0001, IDE0005
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProject.Exp1
{
    using AbsoletlyWrong;
    using TestProject.Exp1;
    using TestProject.Exp1.AbsoletlyWrong;

    internal class Exp1Class6
    {
        public Exp1Class6(
            TestProject.Exp1.Exp1Class1 p1,
            Exp1Class2 p2,
            TestProject.Exp1.Exp1Class3 p3,
            TestProject.Exp1.AbsoletlyWrong.Exp1Class7 p7_1,
            TestProject.Exp1.AbsoletlyWrong.Exp1Class7 p7_2,
            TestProject.Exp1.AbsoletlyWrong.Exp1Class7 p7_3,
            Exp1Class7 p7_4
            )
        {
        }
    }

    namespace AbsoletlyWrong
    {
        internal class Exp1Class7
        {
            public Exp1Class7(
                TestProject.Exp1.Exp1Class1 p1,
                Exp1Class2 p2,
                TestProject.Exp1.Exp1Class3 p3,
                TestProject.Exp1.Exp1Class5 p5_1,
                TestProject.Exp1.Exp1Class5 p5_2,
                Exp1Class5 p5_3
                )
            {
            }
        }
    }
}
