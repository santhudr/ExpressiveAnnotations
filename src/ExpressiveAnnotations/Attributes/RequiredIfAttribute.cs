﻿using System;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using ExpressiveAnnotations.Analysis;

namespace ExpressiveAnnotations.Attributes
{
    /// <summary>
    /// Validation attribute which indicates that annotated field is required when computed result of given logical expression is true.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class RequiredIfAttribute : ValidationAttribute
    {
        private const string _defaultErrorMessage = "The {0} field is required by the following logic: {1}.";
        private Func<object, bool> CachedValidationFunc { get; set; }
        private Parser Parser { get; set; }

        /// <summary>
        /// Gets or sets the logical expression based on which requirement condition is computed. 
        /// </summary>
        public string Expression { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating whether the attribute should allow empty or whitespace strings.
        /// </summary>
        public bool AllowEmptyStrings { get; set; }

        public override object TypeId
        {
            /* From MSDN (msdn.microsoft.com/en-us/library/system.attribute.typeid.aspx, msdn.microsoft.com/en-us/library/6w3a7b50.aspx): 
             * 
             * As implemented, this identifier is merely the Type of the attribute. However, it is intended that the unique identifier be used to 
             * identify two attributes of the same type. 
             * 
             * When you define a custom attribute with AttributeUsageAttribute.AllowMultiple set to true, you must override the Attribute.TypeId 
             * property to make it unique. If all instances of your attribute are unique, override Attribute.TypeId to return the object identity 
             * of your attribute. If only some instances of your attribute are unique, return a value from Attribute.TypeId that would return equality 
             * in those cases. For example, some attributes have a constructor parameter that acts as a unique key. For these attributes, return the 
             * value of the constructor parameter from the Attribute.TypeId property.
             * 
             * To summarize: 
             * TypeId is documented as being a "unique identifier used to identify two attributes of the same type". By default, TypeId is just the 
             * type of the attribute, so when two attributes of the same type are encountered, they're considered "the same" by many frameworks.
             */
            get { return string.Format("{0}[{1}]", GetType().FullName, Regex.Replace(Expression, @"\s+", string.Empty)); } /* distinguishes instances based on provided expressions - that way of TypeId creation is chosen over the alternatives below: 
                                                                                                                            *     - returning new object - it is too much, instances would be always different, 
                                                                                                                            *     - returning hash code based on expression - can lead to collisions (infinitely many strings can't be mapped injectively into any finite set - best unique identifier for string is the string itself) 
                                                                                                                            */
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RequiredIfAttribute" /> class.
        /// </summary>
        /// <param name="expression">The logical expression based on which requirement condition is computed.</param>
        public RequiredIfAttribute(string expression)
            : base(_defaultErrorMessage)
        {
            Parser = new Parser();
            Parser.RegisterMethods();

            Expression = expression;
            AllowEmptyStrings = false;
        }

        /// <summary>
        /// Parses and compiles expression provided to the attribute. Compiled lambda is then cached and used for validation purposes.
        /// </summary>
        /// <param name="validationContextType">The type of the object to be validated.</param>
        /// <param name="force">Flag indicating whether parsing should be rerun despite the fact compiled lambda already exists.</param>
        public void Compile(Type validationContextType, bool force = false)
        {
            if (force)
            {
                CachedValidationFunc = Parser.Parse(validationContextType, Expression);
                return;
            }
            if (CachedValidationFunc == null)
                CachedValidationFunc = Parser.Parse(validationContextType, Expression);
        }

        /// <summary>
        /// Formats the error message.
        /// </summary>
        /// <param name="displayName">The user-visible name of the required field to include in the formatted message.</param>
        /// <param name="expression">The user-visible expression to include in the formatted message.</param>
        /// <returns>
        /// The localized message to present to the user.
        /// </returns>
        public string FormatErrorMessage(string displayName, string expression)
        {
            return string.Format(ErrorMessageString, displayName, expression);
        }

        /// <summary>
        /// Validates the specified value with respect to the current validation attribute.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <param name="validationContext">The context information about the validation operation.</param>
        /// <returns>
        /// An instance of the <see cref="T:System.ComponentModel.DataAnnotations.ValidationResult" /> class.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">validationContext;ValidationContext not provided.</exception>
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (validationContext == null)
                throw new ArgumentNullException("validationContext", "ValidationContext not provided.");

            var isEmpty = value is string && string.IsNullOrWhiteSpace((string) value);
            if (value == null || (isEmpty && !AllowEmptyStrings))
            {
                if (CachedValidationFunc == null)
                    CachedValidationFunc = Parser.Parse(validationContext.ObjectType, Expression);
                if (CachedValidationFunc(validationContext.ObjectInstance)) // check if the requirement condition is satisfied
                    return new ValidationResult(FormatErrorMessage(validationContext.DisplayName, Expression), new[] {validationContext.MemberName}); // requirement confirmed => notify
            }

            return ValidationResult.Success;
        }
    }
}
