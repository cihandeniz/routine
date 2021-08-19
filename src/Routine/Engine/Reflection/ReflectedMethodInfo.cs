﻿using System.Linq;
using Routine.Core.Reflection;

namespace Routine.Engine.Reflection
{
	public class ReflectedMethodInfo : MethodInfo
	{
		internal ReflectedMethodInfo(System.Reflection.MethodInfo methodInfo)
			: base(methodInfo) { }

		protected override MethodInfo Load() { return this; }

		public override ParameterInfo[] GetParameters() { return methodInfo.GetParameters().Select(p => ParameterInfo.Reflected(p)).ToArray(); }
		public override object[] GetCustomAttributes() { return methodInfo.GetCustomAttributes(true); }
		public override object[] GetReturnTypeCustomAttributes() { return methodInfo.ReturnTypeCustomAttributes.GetCustomAttributes(true); }

		public override TypeInfo GetFirstDeclaringType()
		{
			return SearchFirstDeclaringType();
		}

		public override object Invoke(object target, params object[] parameters)
		{
			return new ReflectionMethodInvoker(methodInfo).Invoke(target, parameters);
		}

		public override object InvokeStatic(params object[] parameters)
		{
			return new ReflectionMethodInvoker(methodInfo).Invoke(null, parameters);
		}

		public override string Name => methodInfo.Name;
        public override bool IsPublic => methodInfo.IsPublic;
        public override bool IsStatic => methodInfo.IsStatic;

        public override TypeInfo DeclaringType => TypeInfo.Get(methodInfo.DeclaringType);
        public override TypeInfo ReflectedType => TypeInfo.Get(methodInfo.ReflectedType);
        public override TypeInfo ReturnType => TypeInfo.Get(methodInfo.ReturnType);
    }
}

