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
using System.Diagnostics;
using dnSpy.Debugger.DotNet.Metadata;
using Mono.Debugger.Soft;

namespace dnSpy.Debugger.DotNet.Mono.Impl.Evaluation {
	struct MonoDebugTypeCreator {
		readonly DbgEngineImpl engine;
		readonly TypeCache typeCache;
		int recursionCounter;

		public static TypeMirror GetType(DbgEngineImpl engine, DmdType type) {
			var typeCache = TypeCache.GetOrCreate(type.AppDomain);
			if (typeCache.TryGetType(type, out var monoType))
				return monoType;

			var info = new MonoDebugTypeCreator(engine, typeCache).Create(type);
			monoType = info.type;
			return monoType;
		}

		MonoDebugTypeCreator(DbgEngineImpl engine, TypeCache typeCache) {
			this.engine = engine;
			this.typeCache = typeCache;
			recursionCounter = 0;
		}

		(TypeMirror type, bool containsGenericParameters) Create(DmdType type) {
			if ((object)type == null)
				throw new ArgumentNullException(nameof(type));

			if (typeCache.TryGetType(type, out var cachedType))
				return (cachedType, false);

			if (recursionCounter++ > 100)
				throw new InvalidOperationException();

			(TypeMirror type, bool containsGenericParameters) result;
			bool addType = true;
			switch (type.TypeSignatureKind) {
			case DmdTypeSignatureKind.Type:
				if (!engine.TryGetMonoModule(type.Module.GetDebuggerModule() ?? throw new InvalidOperationException(), out var monoModule))
					throw new InvalidOperationException();
				Debug.Assert((type.MetadataToken >> 24) == 0x02);
				//TODO: It's possible to resolve types, but it's an internal method and it requires a method in the module
				result = (monoModule.Assembly.GetType(type.FullName, false, false), false);
				if (result.type == null)
					throw new InvalidOperationException();
				if (result.type.MetadataToken != type.MetadataToken)
					throw new InvalidOperationException();
				break;

			case DmdTypeSignatureKind.Pointer:
				result = Create(type.GetElementType());
				result = (TryResolveType(result.type, type), result.containsGenericParameters);
				if (result.type == null)
					throw new InvalidOperationException();
				if (!result.type.IsPointer)
					throw new InvalidOperationException();
				break;

			case DmdTypeSignatureKind.ByRef:
				result = Create(type.GetElementType());
				result = (TryResolveType(result.type, type), result.containsGenericParameters);
				if (result.type == null)
					throw new InvalidOperationException();
				// This currently always fails
				//TODO: We could func-eval MakeByRefType()
				if (!result.type.IsByRef)
					throw new InvalidOperationException();
				break;

			case DmdTypeSignatureKind.TypeGenericParameter:
			case DmdTypeSignatureKind.MethodGenericParameter:
				result = (Create(type.AppDomain.System_Object).type, true);
				addType = false;
				break;

			case DmdTypeSignatureKind.SZArray:
				result = Create(type.GetElementType());
				result = (TryResolveType(result.type, type), result.containsGenericParameters);
				if (result.type == null)
					throw new InvalidOperationException();
				if (!result.type.IsArray || result.type.GetArrayRank() != 1 || !result.type.FullName.EndsWith("[]", StringComparison.Ordinal))
					throw new InvalidOperationException();
				break;

			case DmdTypeSignatureKind.MDArray:
				result = Create(type.GetElementType());
				result = (TryResolveType(result.type, type), result.containsGenericParameters);
				if (result.type == null)
					throw new InvalidOperationException();
				if (!result.type.IsArray || (result.type.GetArrayRank() == 1 && result.type.FullName.EndsWith("[]", StringComparison.Ordinal)))
					throw new InvalidOperationException();
				break;

			case DmdTypeSignatureKind.GenericInstance:
				result = Create(type.GetGenericTypeDefinition());
				result = (TryResolveType(result.type, type), result.containsGenericParameters);
				if (result.type == null)
					throw new InvalidOperationException();
				// This fails on Unity (version < 2.12), since it doesn't have that info available
				//if (!result.type.IsGenericType)
				//	throw new InvalidOperationException();
				break;

			case DmdTypeSignatureKind.FunctionPointer:
				// It's not possible to create function pointers, so use a pointer type instead
				result = Create(type.AppDomain.System_Void.MakePointerType());
				addType = false;
				break;

			default:
				throw new InvalidOperationException();
			}

			if (result.type == null)
				throw new InvalidOperationException();
			if (addType && !result.containsGenericParameters)
				typeCache.Add(result.type, type);

			recursionCounter--;
			return result;
		}

		static TypeMirror TryResolveType(TypeMirror monoType, DmdType realType) {
			var fullName = realType.FullName;
			if (fullName == null && realType.IsGenericType)
				fullName = realType.GetGenericTypeDefinition().FullName;
			if (string.IsNullOrEmpty(fullName))
				return null;
			// This fails if fullName is a generic instantiated type and at least one generic argument
			// is a type in another assembly, eg. List<Mytype>.
			//TODO: func-eval could be used to create the type:
			//		Call(monoType.Assembly.GetAssemblyObject(), method_GetType, fullName)
			return monoType.Module.Assembly.GetType(fullName);
		}
	}
}