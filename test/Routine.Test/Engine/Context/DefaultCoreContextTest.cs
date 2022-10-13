using Routine.Engine;
using Routine.Engine.Context;
using Routine.Engine.Reflection;
using Routine.Test.Core;
using Routine.Test.Engine.Context.Domain;

namespace Routine.Test.Engine.Context;

[TestFixture]
public class DefaultCoreContextTest : CoreTestBase
{
    private ICoreContext testing;

    [SetUp]
    public override void SetUp()
    {
        base.SetUp();

        var codingStyle = BuildRoutine.CodingStyle().FromBasic()
            .AddTypes(GetType().Assembly, t => !string.IsNullOrEmpty(t.Namespace) && t.Namespace.StartsWith("Routine.Test.Engine.Context.Domain"))
            .IdExtractor.Set(c => c.IdByProperty(m => m.Returns<string>("Id")))
            .Locator.Set(c => c.Locator(l => l.Constant(null)))
            .ValueExtractor.Set(c => c.Value(e => e.By(obj => $"{obj}")));

        testing = new DefaultCoreContext(codingStyle);
    }

    [Test]
    public void Cannot_access_a_domain_type_before_context_is_initialized()
    {
        Assert.Throws<InvalidOperationException>(() => { var _ = testing.DomainTypes; });
        Assert.Throws<InvalidOperationException>(() => testing.GetDomainType(type.of<CachedBusiness>()));
    }

    [Test]
    public void Caches_domain_types_by_object_model_id()
    {
        testing.BuildDomainTypes();

        var domainType = testing.GetDomainType(type.of<CachedBusiness>());

        var expected = testing.GetDomainType(domainType.Id);
        var actual = testing.GetDomainType(domainType.Id);

        Assert.AreSame(expected, actual);
    }

    [Test]
    public void Build_domain_types_rebuilds_everytime_it_is_called()
    {
        testing.BuildDomainTypes();
        var oldDomainType = testing.GetDomainType(type.of<CachedBusiness>());

        testing.BuildDomainTypes();
        var newDomainType = testing.GetDomainType(type.of<CachedBusiness>());

        Assert.AreNotSame(oldDomainType, newDomainType);
    }

    [Test]
    public void Adding_a_type_later_on_is_reflected_over_existing_proxy_types_of_existing_members()
    {
        var codingStyle = BuildRoutine.CodingStyle().FromBasic().AddTypes(typeof(CachedBusiness));
        var proxyOverAProperty = (ProxyTypeInfo)type.of<CachedBusiness>().GetProperty(nameof(CachedBusiness.LaterAddedType)).PropertyType;

        Assert.IsInstanceOf<ReflectedTypeInfo>(proxyOverAProperty.Real);

        codingStyle.AddTypes(typeof(LaterAddedType));

        Assert.IsInstanceOf<OptimizedTypeInfo>(proxyOverAProperty.Real);
    }
}
