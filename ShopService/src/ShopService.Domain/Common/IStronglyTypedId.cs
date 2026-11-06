namespace ShopService.Domain.Common;

public interface IStronglyTypedId<TSelf> where TSelf : struct, IStronglyTypedId<TSelf>
{
    Guid Value { get; }

    static abstract TSelf From(Guid value);
}

public static class StronglyTypedIds
{
    public static bool IsStronglyTypedId(Type type) =>
        type.IsValueType && type.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStronglyTypedId<>));
}
