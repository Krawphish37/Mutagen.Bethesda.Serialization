using System.Reflection;
using Loqui;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Serialization.Newtonsoft;
using Shouldly;

namespace Mutagen.Bethesda.Serialization.Tests.C59;

// C76 (SFSearch S170): the registry listed the closed generic classes found as property types only
// (ObjectTemplate<Weapon.Property>), not the ones an abstract closed class stands for: AObjectModProperty<Weapon.Property>
// holds ObjectModIntProperty<Weapon.Property>, ObjectModFloatProperty<...>, ObjectModFormLinkIntProperty<...>, ... for
// every property enum the records use. Without them TryGetFieldDefaults had nothing for those objects. Now the registry
// follows each closed class to the generic classes deriving from it and to its own properties.
public class ClosedGenericRegistryTests
{
    private static readonly ISerializationRegistry Registry = Fallout4SerializationRegistry.Instance;

    /// <summary>
    /// Every closed generic Loqui class a Fallout4 class can hold, by reflection: the property types of every Loqui
    /// class, then of each closed class found, and the generic classes deriving from each (groups aside: the registry leaves them out)
    /// </summary>
    internal static IReadOnlyList<Type> Reachable()
    {
        var assembly = typeof(Fallout4Mod).Assembly;
        bool IsLoquiClass(Type t) => t.IsClass && t.Assembly == assembly && typeof(ILoquiObject).IsAssignableFrom(t)
            && !t.GetInterfaces().Any(i => i.Name.StartsWith("IGroupGetter") || i.Name.StartsWith("IListGroupGetter"));
        var genericClasses = assembly.GetTypes().Where(t => t.IsGenericTypeDefinition && IsLoquiClass(t)).ToArray();
        var queue = new Queue<Type>();
        void HeldBy(Type holder)
        {
            foreach (var prop in holder.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                foreach (var t in Constructed(prop.PropertyType)) queue.Enqueue(t);
            }
        }
        foreach (var t in assembly.GetTypes().Where(t => !t.IsGenericType && IsLoquiClass(t))) HeldBy(t);
        var seen = new HashSet<Type>();
        var found = new List<Type>();
        while (queue.Count > 0)
        {
            var t = queue.Dequeue();
            if (!IsLoquiClass(t) || t.ContainsGenericParameters || !seen.Add(t)) continue;
            HeldBy(t);
            foreach (var g in genericClasses)
            {
                for (var b = g.BaseType; b != null; b = b.BaseType)
                {
                    if (!b.IsGenericType || b.GetGenericTypeDefinition() != t.GetGenericTypeDefinition()) continue;
                    if (b.GetGenericArguments().SequenceEqual(g.GetGenericArguments())) queue.Enqueue(g.MakeGenericType(t.GetGenericArguments()));
                    break;
                }
            }
            if (!t.IsAbstract) found.Add(t);
        }
        return found;
    }

    private static IEnumerable<Type> Constructed(Type t)
    {
        if (t.IsArray)
        {
            foreach (var x in Constructed(t.GetElementType()!)) yield return x;
            yield break;
        }
        if (!t.IsGenericType) yield break;
        yield return t;
        foreach (var arg in t.GetGenericArguments())
        {
            foreach (var x in Constructed(arg)) yield return x;
        }
    }

    [Fact]
    public void EveryReachableClosedGenericIsListedWithItsDefaults()
    {
        var reachable = Reachable();
        reachable.Count(t => t.Name.StartsWith("ObjectMod")).ShouldBeGreaterThan(10);
        reachable.ShouldContain(typeof(ObjectModIntProperty<Weapon.Property>));
        reachable.ShouldContain(typeof(ObjectModFloatProperty<Armor.Property>));
        var converter = new JsonObjectConverter(Registry, RegistryTests.Meta);
        var checkedDefaults = 0;
        foreach (var t in reachable)
        {
            Registry.ClassTypes.ShouldContain(t, t.ToString());
            LoquiRegistration.TryGetRegister(t.GetGenericTypeDefinition(), out var registration).ShouldBeTrue(t.ToString());
            Registry.TryGetClassType(registration!.GetterType.MakeGenericType(t.GetGenericArguments()), out var byGetter).ShouldBeTrue(t.ToString());
            byGetter.ShouldBe(t);
            Registry.TryGetFieldDefaults(t, out var defaults).ShouldBeTrue(t.ToString());

            // An empty object reads as the listed defaults, and a new one round-trips by its runtime type
            var empty = converter.FromJson("{}", t);
            foreach (var (name, value) in defaults!)
            {
                var prop = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (prop == null || value == null) continue;
                var actual = prop.GetValue(empty);
                if (actual is Mutagen.Bethesda.Plugins.IFormLinkIdentifier link)
                {
                    // a link field's default is listed as its FormKey
                    link.FormKey.ShouldBe(value, $"{t} {name}");
                }
                else
                {
                    actual.ShouldBe(value, $"{t} {name}");
                }
                checkedDefaults++;
            }
            var json = converter.ToJson(System.Activator.CreateInstance(t)!);
            converter.ToJson(converter.FromJson(json, t)).ShouldBe(json, t.ToString());
        }
        // The closed generic classes the registry lists are exactly these
        Registry.ClassTypes.Where(t => t.IsGenericType).OrderBy(t => t.ToString())
            .ShouldBe(reachable.OrderBy(t => t.ToString()));
        checkedDefaults.ShouldBeGreaterThan(reachable.Count);
    }
}
