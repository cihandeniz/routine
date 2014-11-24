using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Routine.Engine;
using Routine.Engine.Reflection;

namespace Routine
{
	public abstract class TypeInfo : IType
	{
		#region Factory Methods

		private static readonly Dictionary<Type, TypeInfo> typeCache;
		private static readonly List<Type> domainTypes;

		private static Func<Type, bool> proxyMatcher;
		private static Func<Type, Type> actualTypeGetter;

		static TypeInfo()
		{
			typeCache = new Dictionary<Type, TypeInfo>();
			domainTypes = new List<Type>();

			SetProxyMatcher(null, null);
		}

		public static void Clear()
		{
			typeCache.Clear();
			domainTypes.Clear();

			SetProxyMatcher(null, null);
		}

		public static List<TypeInfo> GetDomainTypes()
		{
			return domainTypes.Select(t => t.ToTypeInfo()).ToList();
		}

		public static void AddDomainTypes(params Type[] newDomainTypes)
		{
			domainTypes.AddRange(newDomainTypes.Where(t => !domainTypes.Contains(t)));
			
			typeCache.Clear();
		}

		public static void SetProxyMatcher(Func<Type, bool> proxyMatcher, Func<Type, Type> actualTypeGetter)
		{
			if (proxyMatcher == null) { proxyMatcher = t => false; }
			if (actualTypeGetter == null) { actualTypeGetter = t => t; }

			TypeInfo.proxyMatcher = proxyMatcher;
			TypeInfo.actualTypeGetter = actualTypeGetter;
		}

		protected const System.Reflection.BindingFlags ALL_STATIC = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
		protected const System.Reflection.BindingFlags ALL_INSTANCE = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;

		public static TypeInfo Void()
		{
			return Get(typeof(void));
		}

		public static TypeInfo Get<T>()
		{
			return Get(typeof(T));
		}

		public static TypeInfo Get(Type type)
		{
			if (type == null)
			{
				return null;
			}

			TypeInfo result;

			if (!typeCache.TryGetValue(type, out result))
			{
				lock (typeCache)
				{
					if (!typeCache.TryGetValue(type, out result))
					{
						if (proxyMatcher(type))
						{
							var actualType = actualTypeGetter(type);

							if (!typeCache.TryGetValue(actualType, out result))
							{
								result = CreateTypeInfo(actualType);

								typeCache.Add(actualType, result);

								result.Load();
							}

							typeCache.Add(type, result);
						}
						else
						{
							result = CreateTypeInfo(type);

							typeCache.Add(type, result);

							result.Load();
						}
					}
				}
			}

			return result;
		}

		private static TypeInfo CreateTypeInfo(Type type)
		{
			TypeInfo result;

			if (type == typeof(void))
			{
				result = new VoidTypeInfo();
			}
			else if (type.GetMethod("Parse", new[] { typeof(string) }) != null && type.GetMethod("Parse", new[] { typeof(string) }).ReturnType == type)
			{
				result = new ParseableTypeInfo(type);
			}
			else if (type.IsArray)
			{
				result = new ArrayTypeInfo(type);
			}
			else if (type.IsEnum)
			{
				result = new EnumTypeInfo(type);
			}
			else if (type.ContainsGenericParameters)
			{
				result = new ReflectedTypeInfo(type);
			}
			else if (domainTypes.Contains(type))
			{
				result = new DomainTypeInfo(type);
			}
			else
			{
				result = new ReflectedTypeInfo(type);
			}

			return result;
		}

		#endregion

		protected readonly Type type;
		protected TypeInfo(Type type)
		{
			this.type = type;

			IsPublic = type.IsPublic;
			IsAbstract = type.IsAbstract;
			IsInterface = type.IsInterface;
			IsValueType = type.IsValueType;
			IsGenericType = type.IsGenericType;
			IsPrimitive = type.IsPrimitive;
		}

		public Type GetActualType() { return type; }

		public bool IsPublic { get; protected set; }
		public bool IsAbstract { get; protected set; }
		public bool IsInterface { get; protected set; }
		public bool IsValueType { get; protected set; }
		public bool IsGenericType { get; protected set; }
		public bool IsPrimitive { get; protected set; }

		public bool IsVoid { get; protected set; }
		public bool IsEnum { get; protected set; }
		public bool IsArray { get; protected set; }
		public bool IsDomainType { get; protected set; }

		public abstract string Name { get; }
		public abstract string FullName { get; }
		public abstract string Namespace { get; }
		public abstract TypeInfo BaseType { get; }

		public abstract ConstructorInfo[] GetAllConstructors();
		public abstract PropertyInfo[] GetAllProperties();
		public abstract PropertyInfo[] GetAllStaticProperties();
		public abstract MethodInfo[] GetAllMethods();
		public abstract MethodInfo[] GetAllStaticMethods();
		public abstract object[] GetCustomAttributes();
		protected abstract TypeInfo[] GetGenericArguments();
		protected abstract TypeInfo GetElementType();
		protected abstract TypeInfo[] GetInterfaces();
		public abstract bool CanBe(TypeInfo other);
		protected abstract TypeInfo[] GetConvertibleTypes();
		public virtual List<string> GetEnumNames() { return new List<string>(); }
		public virtual List<object> GetEnumValues() { return new List<object>(); }
		protected virtual TypeInfo GetEnumUnderlyingType() { return null; }

		protected abstract MethodInfo GetParseMethod();
		protected abstract void Load();

		public abstract object CreateInstance();
		public abstract IList CreateListInstance(int length);

		public virtual List<ConstructorInfo> GetPublicConstructors()
		{
			return GetAllConstructors().Where(c => c.IsPublic).ToList();
		}

		public virtual ConstructorInfo GetConstructor(params TypeInfo[] typeInfos)
		{
			if (typeInfos.Length > 0)
			{
				var first = typeInfos[0];
				var rest = Enumerable.Range(1, typeInfos.Length - 1).Select(i => typeInfos[i]).ToArray();

				return GetAllConstructors().SingleOrDefault(c => c.HasParameters(first, rest));
			}

			return GetAllConstructors().SingleOrDefault(c => c.HasNoParameters());
		}

		public virtual ICollection<PropertyInfo> GetPublicProperties() { return GetPublicProperties(false); }
		public virtual ICollection<PropertyInfo> GetPublicProperties(bool onlyPublicReadableAndWritables)
		{
			if (onlyPublicReadableAndWritables)
			{
				return GetAllProperties().Where(p => p.IsPubliclyReadable && p.IsPubliclyWritable).ToList();
			}

			return GetAllProperties().Where(p => p.IsPubliclyReadable).ToList();
		}

		public virtual ICollection<PropertyInfo> GetPublicStaticProperties() { return GetPublicStaticProperties(false); }
		public virtual ICollection<PropertyInfo> GetPublicStaticProperties(bool onlyPublicReadableAndWritables)
		{
			if (onlyPublicReadableAndWritables)
			{
				return GetAllStaticProperties().Where(p => p.IsPubliclyReadable && p.IsPubliclyWritable).ToList();
			}

			return GetAllStaticProperties().Where(p => p.IsPubliclyReadable).ToList();
		}

		public virtual PropertyInfo GetProperty(string name)
		{
			return GetAllProperties().SingleOrDefault(p => p.Name == name);
		}

		public virtual List<PropertyInfo> GetProperties(string name)
		{
			return GetAllProperties().Where(p => p.Name == name).ToList();
		}

		public virtual PropertyInfo GetStaticProperty(string name)
		{
			return GetAllStaticProperties().SingleOrDefault(p => p.Name == name);
		}

		public virtual List<PropertyInfo> GetStaticProperties(string name)
		{
			return GetAllStaticProperties().Where(p => p.Name == name).ToList();
		}

		public virtual ICollection<MethodInfo> GetPublicMethods()
		{
			return GetAllMethods().Where(m => m.IsPublic).ToList();
		}

		public virtual ICollection<MethodInfo> GetPublicStaticMethods()
		{
			return GetAllStaticMethods().Where(m => m.IsPublic).ToList();
		}

		public virtual MethodInfo GetMethod(string name)
		{
			return GetAllMethods().SingleOrDefault(m => m.Name == name);
		}

		public virtual List<MethodInfo> GetMethods(string name)
		{
			return GetAllMethods().Where(m => m.Name == name).ToList();
		}

		public virtual MethodInfo GetStaticMethod(string name)
		{
			return GetAllStaticMethods().SingleOrDefault(m => m.Name == name);
		}

		public virtual List<MethodInfo> GetStaticMethods(string name)
		{
			return GetAllStaticMethods().Where(m => m.Name == name).ToList();
		}

		public override string ToString()
		{
			return type.ToString();
		}

		public static bool operator ==(TypeInfo l, TypeInfo r) { return Equals(l, r); }
		public static bool operator !=(TypeInfo l, TypeInfo r) { return !(l == r); }

		public override bool Equals(object obj)
		{
			if (obj == null) { return false; }

			var typeObj = obj as Type;
			if (typeObj != null) { return type == typeObj; }

			var typeInfoObj = obj as TypeInfo;
			if (typeInfoObj == null) { return false; }

			return ReferenceEquals(this, typeInfoObj) || type == typeInfoObj.type;
		}

		public override int GetHashCode()
		{
			return type.GetHashCode();
		}

		#region ITypeComponent implementation

		IType ITypeComponent.ParentType { get { return null; } }

		#endregion

		#region IType implementation

		IType IType.BaseType { get { return BaseType; } }

		List<IType> IType.ConvertibleTypes { get { return GetConvertibleTypes().Cast<IType>().ToList(); } }
		List<IInitializer> IType.Initializers { get { return GetAllConstructors().Cast<IInitializer>().ToList(); } }
		List<IMember> IType.Members { get { return GetAllProperties().Where(p => !p.IsIndexer).Cast<IMember>().ToList(); } }
		List<IOperation> IType.Operations { get { return GetAllMethods().Cast<IOperation>().ToList(); } }

		List<IType> IType.GetGenericArguments() { return GetGenericArguments().Cast<IType>().ToList(); }
		IType IType.GetElementType() { return GetElementType(); }
		IOperation IType.GetParseOperation() { return GetParseMethod(); }
		IType IType.GetEnumUnderlyingType() { return GetEnumUnderlyingType(); }

		bool IType.CanBe(IType otherType)
		{
			var otherTypeInfo = otherType as TypeInfo;

			return otherTypeInfo != null && CanBe(otherTypeInfo);
		}

		object IType.Convert(object target, IType otherType)
		{
			var thisAsIType = this as IType;

			if (!thisAsIType.CanBe(otherType)) { throw new InvalidOperationException(string.Format("Cannot convert an object of type {0} to {1}", this, otherType)); }

			return target;
		}

		#endregion
	}

	public class NoDomainTypeRootNamespaceIsDefinedException : Exception { }

	// ReSharper disable InconsistentNaming
	public static class type
	{
		public static TypeInfo of<T>()
		{
			return TypeInfo.Get<T>();
		}

		public static TypeInfo ofvoid()
		{
			return TypeInfo.Void();
		}
	}
	// ReSharper restore InconsistentNaming

	public static class TypeInfoObjectExtensions
	{
		public static TypeInfo GetTypeInfo(this object source)
		{
			if (source == null) { return null; }

			return TypeInfo.Get(source.GetType());
		}

		public static TypeInfo ToTypeInfo(this Type source)
		{
			return TypeInfo.Get(source);
		}
	}

}

