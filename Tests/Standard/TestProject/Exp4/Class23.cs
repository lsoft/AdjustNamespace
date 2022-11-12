#pragma warning disable IDE0001, IDE0005
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProject.Exp4
{
    internal class Class2
    {
        public SubjectWrong.Exp4.MyEnum Setting => SubjectWrong.Exp4.MyEnum.MustBeCrossCluster;
    }
}

namespace TestProject.Exp4
{
    using SubjectWrong.Exp4;

    internal class Class3
    {
        public MyEnum Setting => MyEnum.MustBeCrossCluster;
    }
}

