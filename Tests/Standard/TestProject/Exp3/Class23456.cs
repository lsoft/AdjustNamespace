#pragma warning disable IDE0001, IDE0005
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProject.Exp3
{
    internal class Class2<T>
    {
    }
}

namespace TestProject.Exp3
{
    internal class Class3 : Class2<SubjectWrong.Exp3.Class1>
    {
    }
}

namespace TestProject.Exp3
{
    using SubjectWrong.Exp3;

    internal class Class4 : Class2<Class1>
    {
    }
}

namespace TestProject.Exp3
{
    internal class Class5<T> : Class2<T>
        where T : SubjectWrong.Exp3.Class1
    {
    }
}

namespace TestProject.Exp3
{
    using SubjectWrong.Exp3;

    internal class Class6<T> : Class2<T>
        where T : Class1
    {
    }
}
