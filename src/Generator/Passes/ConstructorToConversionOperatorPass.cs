﻿using CppSharp.AST;
using CppSharp.AST.Extensions;

namespace CppSharp.Passes
{
    /// <summary>
    /// This pass will create conversion operators out of single argument
    /// constructors.
    /// </summary>
    public class ConstructorToConversionOperatorPass : TranslationUnitPass
    {
        public override bool VisitMethodDecl(Method method)
        {
            if (AlreadyVisited(method) || !method.IsGenerated || !method.IsConstructor ||
                method.IsCopyConstructor || method.Parameters.Count != 1)
                return false;

            var parameter = method.Parameters[0];
            // TODO: disable implicit operators for C++/CLI because they seem not to be support parameters
            if (!Driver.Options.IsCSharpGenerator)
            {
                var pointerType = parameter.Type as PointerType;
                if (pointerType != null && !pointerType.IsReference)
                    return false;
            }
            var qualifiedPointee = parameter.Type.GetFinalPointee() ?? parameter.Type;
            Class castFromClass;
            var castToClass = method.OriginalNamespace as Class;
            if (qualifiedPointee.TryGetClass(out castFromClass))
            {
                if (castToClass == null)
                    return false;
                if (castFromClass == castToClass)
                    return false;
            }
            if (castToClass != null && castToClass.IsAbstract)
                return false;
            var operatorKind = method.IsExplicit
                ? CXXOperatorKind.ExplicitConversion
                : CXXOperatorKind.Conversion;
            var qualifiedCastToType = new QualifiedType(new TagType(method.Namespace));
            var conversionOperator = new Method
            {
                Name = Operators.GetOperatorIdentifier(operatorKind),
                Namespace = method.Namespace,
                Kind = CXXMethodKind.Conversion,
                SynthKind = FunctionSynthKind.ComplementOperator,
                ConversionType = qualifiedCastToType,
                ReturnType = qualifiedCastToType,
                OperatorKind = operatorKind,
                IsExplicit = method.IsExplicit
            };
            var p = new Parameter(parameter);
            Class @class;
            if (p.Type.SkipPointerRefs().TryGetClass(out @class))
                p.QualifiedType = new QualifiedType(new TagType(@class), parameter.QualifiedType.Qualifiers);
            p.DefaultArgument = null;
            conversionOperator.Parameters.Add(p);
            ((Class) method.Namespace).Methods.Add(conversionOperator);
            return true;
        }
    }
}
