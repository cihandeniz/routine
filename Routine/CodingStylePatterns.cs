using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Routine.Core.Configuration;
using Routine.Engine.Configuration.Conventional;
using Routine.Engine.Virtual;

namespace Routine
{
	public static class CodingStylePatterns
	{
		public static ConventionalCodingStyle FromEmpty(this PatternBuilder<ConventionalCodingStyle> source) { return new ConventionalCodingStyle(); }

		public static ConventionalCodingStyle ParseableValueTypePattern(this PatternBuilder<ConventionalCodingStyle> source)
		{
			return source
					.FromEmpty()

					.TypeIsValue.Set(c => c.Constant(true).When(t => t.CanBe<string>() || t.CanParse()))

					.StaticInstances.Set(c => c.Constant(true, false).When(t => t.CanBe<bool>()))

					.IdExtractor.Set(c => c.Id(e => e.By(o => string.Format("{0}", o))).When(t => t.CanBe<string>() || t.CanParse()))

					.ObjectLocator.Set(c => c.Locator(l => l.By(o => string.Format("{0}", o))).When(t => t.CanBe<string>()))
					.ObjectLocator.Set(c => c.Locator(l => l.By((t, id) => t.Parse(id))).When(t => t.CanParse()))

					.Members.AddNoneWhen(t => t.CanBe<string>() || t.CanParse())
					.Operations.AddNoneWhen(t => t.CanBe<string>() || t.CanParse())
					;
		}

		public static ConventionalCodingStyle EnumPattern(this PatternBuilder<ConventionalCodingStyle> source) { return source.EnumPattern(true); }
		public static ConventionalCodingStyle EnumPattern(this PatternBuilder<ConventionalCodingStyle> source, bool useName)
		{
			if (useName)
			{
				return source
					.FromEmpty()
					.TypeIsValue.Set(c => c.Constant(true).When(t => t.IsEnum))
					.StaticInstances.Set(c => c.By(t => t.GetEnumValues()).When(t => t.IsEnum))
					.IdExtractor.Set(c => c.Id(e => e.By(o => o.ToString())).When(t => t.IsEnum))
					.ObjectLocator.Set(c => c.Locator(l => l.By((t, id) => t.GetEnumValues()[t.GetEnumNames().IndexOf(id)]).AcceptNullResult(false)).When(t => t.IsEnum))
					.Members.AddNoneWhen(t => t.IsEnum)
					.Operations.AddNoneWhen(t => t.IsEnum)
					;
			}

			return source
					.FromEmpty()
					.TypeIsValue.Set(false, t => t.IsEnum)
					.StaticInstances.Set(c => c.By(t => t.GetEnumValues()).When(t => t.IsEnum))
					.IdExtractor.Set(c => c.Id(e => e.By(o => ((int)o).ToString(CultureInfo.InvariantCulture))).When(t => t.IsEnum))
					.ValueExtractor.Set(c => c.Value(e => e.By(o => o.ToString())).When(t => t.IsEnum))
					.ObjectLocator.Set(c => c.Locator(l => l.By((t, id) =>
					{
						var value = int.Parse(id);
						var type = t as TypeInfo;
						if (!Enum.IsDefined(type.GetActualType(), value))
						{
							throw new InvalidEnumArgumentException(id, value, type.GetActualType());
						}

						return Enum.ToObject(type.GetActualType(), value);
					}).AcceptNullResult(false)).When(t => t is TypeInfo && t.IsEnum))
					.Members.AddNoneWhen(t => t.IsEnum)
					.Operations.AddNoneWhen(t => t.IsEnum)
					;
		}

		public static ConventionalCodingStyle ShortModelIdPattern(this PatternBuilder<ConventionalCodingStyle> source, string prefix, string shortPrefix)
		{
			return source
					.FromEmpty()
					.TypeId.Set(c => c
						.By(t => t.FullName.ShortenModelId(prefix, shortPrefix))
						.When(t => t.FullName.StartsWith(prefix + ".") && t.IsPublic));
		}

		public static string ShortenModelId(this string source, string actualPrefix, string shortPrefix)
		{
			shortPrefix = shortPrefix.Append("-");
			actualPrefix = actualPrefix.Append(".");

			return shortPrefix.Append(source.After(actualPrefix).SplitCamelCase('-').Replace("-.-", "--").ToLowerInvariant());
		}

		public static string NormalizeModelId(this string source, string actualPrefix, string shortPrefix)
		{
			shortPrefix = shortPrefix.Append("-");
			actualPrefix = actualPrefix.Append(".");

			return actualPrefix.Append(source.After(shortPrefix).Replace("--", "-.-").SnakeCaseToCamelCase('-').ToUpperInitial());
		}

		public static ConventionalCodingStyle AutoMarkWithAttributesPattern(this PatternBuilder<ConventionalCodingStyle> source)
		{
			return source
				.FromEmpty()
				.TypeMarks.Add(c => c.By(t => t.GetCustomAttributes().Select(a => a.GetType().Name.BeforeLast("Attribute")).ToList()))
				.InitializerMarks.Add(s => s.By(i => i.GetCustomAttributes().Select(a => a.GetType().Name.BeforeLast("Attribute")).ToList()))
				.MemberMarks.Add(s => s.By(m => m.GetCustomAttributes().Select(a => a.GetType().Name.BeforeLast("Attribute")).ToList()))
				.OperationMarks.Add(s => s.By(o => o.GetCustomAttributes().Select(a => a.GetType().Name.BeforeLast("Attribute")).ToList()))
				.ParameterMarks.Add(s => s.By(p => p.GetCustomAttributes().Select(a => a.GetType().Name.BeforeLast("Attribute")).ToList()))
				;
		}

		public static ConventionalCodingStyle VirtualTypePattern(this PatternBuilder<ConventionalCodingStyle> source) { return source.VirtualTypePattern(Constants.DEFAULT_VIRTUAL_MARK); }
		public static ConventionalCodingStyle VirtualTypePattern(this PatternBuilder<ConventionalCodingStyle> source, string virtualMark)
		{
			return source
				.FromEmpty()
				.Type.Set(c => c.By(o => ((VirtualObject)o).Type).When(o => o is VirtualObject))
				.IdExtractor.Set(c => c.Id(e => e.By(o => (o as VirtualObject).Id)).When(t => t is VirtualType))
				.ObjectLocator.Set(c => c.Locator(l => l.By((t, id) => new VirtualObject(id, t as VirtualType))).When(t => t is VirtualType))
				.ValueExtractor.Set(c => c.Value(e => e.By(o => string.Format("{0}", o))).When(t => t is VirtualType))
				.TypeMarks.Add(virtualMark, t => t is VirtualType)
			;
		}
	}
}

