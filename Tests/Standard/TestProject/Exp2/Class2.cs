﻿#pragma warning disable IDE0001, IDE0005
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProject.Exp2
{
    internal class Class2 : SubjectWrong.Exp2.Class1
    {
    }
}

namespace TestProject.Exp2
{
    using SubjectWrong.Exp2;

    internal class Class3 : Class1
    {
    }
}
