﻿/*
    Copyright (C) 2014-2017 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace dnSpy.Debugger.DotNet.Metadata.Impl {
	abstract class DmdMethodDef : DmdMethodInfoBase {
		public sealed override DmdType DeclaringType { get; }
		public sealed override DmdType ReflectedType { get; }
		public sealed override int MetadataToken => (int)(0x06000000 + rid);
		public sealed override bool IsMetadataReference => false;

		public sealed override bool IsGenericMethodDefinition => GetMethodSignature().GenericParameterCount != 0;
		public sealed override bool IsGenericMethod => GetMethodSignature().GenericParameterCount != 0;

		public sealed override DmdParameterInfo ReturnParameter {
			get {
				if ((object)__returnParameter_DONT_USE == null)
					InitializeParameters();
				return __returnParameter_DONT_USE;
			}
		}

		protected uint Rid => rid;
		readonly uint rid;

		protected DmdMethodDef(uint rid, DmdType declaringType, DmdType reflectedType) {
			this.rid = rid;
			DeclaringType = declaringType ?? throw new ArgumentNullException(nameof(declaringType));
			ReflectedType = reflectedType ?? throw new ArgumentNullException(nameof(reflectedType));
		}

		public sealed override DmdMethodInfo Resolve(bool throwOnError) => this;

		protected abstract DmdType[] CreateGenericParameters();
		public sealed override ReadOnlyCollection<DmdType> GetGenericArguments() {
			if (__genericParameters_DONT_USE != null)
				return __genericParameters_DONT_USE;
			lock (LockObject) {
				if (__genericParameters_DONT_USE != null)
					return __genericParameters_DONT_USE;
				var res = CreateGenericParameters();
				__genericParameters_DONT_USE = ReadOnlyCollectionHelpers.Create(res);
				return __genericParameters_DONT_USE;
			}
		}
		ReadOnlyCollection<DmdType> __genericParameters_DONT_USE;

		public sealed override ReadOnlyCollection<DmdParameterInfo> GetParameters() {
			if (__parameters_DONT_USE == null)
				InitializeParameters();
			return __parameters_DONT_USE;
		}
		void InitializeParameters() {
			if (__parameters_DONT_USE != null)
				return;
			lock (LockObject) {
				if (__parameters_DONT_USE != null)
					return;
				var info = CreateParameters();
				Debug.Assert(info.parameters.Length == GetMethodSignature().GetParameterTypes().Count);
				__returnParameter_DONT_USE = info.returnParameter;
				__parameters_DONT_USE = ReadOnlyCollectionHelpers.Create(info.parameters);
			}
		}
		ReadOnlyCollection<DmdParameterInfo> __parameters_DONT_USE;
		DmdParameterInfo __returnParameter_DONT_USE;
		protected abstract (DmdParameterInfo returnParameter, DmdParameterInfo[] parameters) CreateParameters();

		internal override DmdMethodInfo GetParentDefinition() {
			((DmdTypeBase)ReflectedType).InitializeParentDefinitions();
			return parentDefinition;
		}

		internal DmdMethodInfo SetParentDefinition(DmdMethodInfo method) => parentDefinition = method;
		DmdMethodInfo parentDefinition;

		public sealed override DmdMethodInfo GetGenericMethodDefinition() {
			if (!IsGenericMethodDefinition)
				throw new InvalidOperationException();
			if ((object)ReflectedType == DeclaringType)
				return this;
			return DeclaringType.GetMethod(MetadataToken) as DmdMethodInfo ?? throw new InvalidOperationException();
		}

		public sealed override DmdMethodInfo MakeGenericMethod(IList<DmdType> typeArguments) => AppDomain.MakeGenericMethod(this, typeArguments);

		public sealed override IList<DmdCustomAttributeData> GetCustomAttributesData() {
			if (__customAttributes_DONT_USE != null)
				return __customAttributes_DONT_USE;
			lock (LockObject) {
				if (__customAttributes_DONT_USE != null)
					return __customAttributes_DONT_USE;
				var info = CreateCustomAttributes();
				__customAttributes_DONT_USE = CustomAttributesHelper.AddPseudoCustomAttributes(this, info.cas, info.implMap);
				return __customAttributes_DONT_USE;
			}
		}
		ReadOnlyCollection<DmdCustomAttributeData> __customAttributes_DONT_USE;

		protected abstract (DmdCustomAttributeData[] cas, DmdImplMap? implMap) CreateCustomAttributes();
	}
}
