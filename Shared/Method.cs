public abstract class Method
{
    public abstract string GetName();
}

public class SimpleMethod : Method
{
    private string _name;

    public SimpleMethod(string name)
    {
        _name = name;
    }

    public override string GetName()
    {
        return _name;
    }
}

public class ComplexMethod : Method
{
    private string _name;
    public Dictionary<string, object> Parameters { get; private set; }

    public ComplexMethod(string name, Dictionary<string, object> parameters)
    {
        _name = name;
        Parameters = parameters;
    }

    public override string GetName()
    {
        return _name;
    }
}

public class MethodContainer
{
    public List<Method> Methods { get; private set; }

    public MethodContainer()
    {
        Methods = new List<Method>();
    }

    public void AddMethod(Method method)
    {
        Methods.Add(method);
    }
}
