﻿// <copyright file="ConfigVarPanel.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Basics.Editor.ConfigVars
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using BovineLabs.Basics.ConfigVars;
    using BovineLabs.Basics.Editor.Settings;
    using Unity.Collections;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary> A panel that draws a collection of config vars. </summary>
    public sealed class ConfigVarPanel : ISettingsPanel
    {
        /// <summary> Initializes a new instance of the <see cref="ConfigVarPanel"/> class. </summary>
        /// <param name="displayName"> The display name of the panel. </param>
        public ConfigVarPanel(string displayName)
        {
            this.DisplayName = displayName;
        }

        /// <inheritdoc/>
        public string DisplayName { get; }

        /// <summary> Gets a list of all the config vars this panel draws. </summary>
        internal List<(ConfigVarAttribute ConfigVar, IConfigVarContainer Container)> ConfigVars { get; }
            = new List<(ConfigVarAttribute, IConfigVarContainer)>();

        /// <inheritdoc/>
        void ISettingsPanel.OnActivate(string searchContext, VisualElement rootElement)
        {
            // Matching the display name should show everything
            var allMatch = string.IsNullOrWhiteSpace(searchContext);

            foreach (var (attribute, container) in this.ConfigVars)
            {
                var readOnly = attribute.IsReadOnly && EditorApplication.isPlaying;
                var field = CreateVisualElement(attribute, container);

                // var.OnChanged += () => field.SetValueWithoutNotify(var.Value);

                // TODO move to uss
                if (!allMatch && MatchesSearchContext(attribute.Name, searchContext))
                {
                    field.style.backgroundColor = ConfigVarStyle.Style.HighlightColor;
                }

                if (readOnly)
                {
                    // TODO
                    // field.style.color = new StyleColor();
                }

                rootElement.Add(field);
            }
        }

        /// <inheritdoc/>
        void ISettingsPanel.OnDeactivate()
        {
        }

        /// <inheritdoc/>
        bool ISettingsPanel.MatchesFilter(string searchContext)
        {
            return this.ConfigVars.Any(s => MatchesSearchContext(s.ConfigVar.Name, searchContext));
        }

        private static bool MatchesSearchContext(string s, string searchContext)
        {
            return s.IndexOf(searchContext, StringComparison.InvariantCultureIgnoreCase) >= 0;
        }

        private static VisualElement CreateVisualElement(ConfigVarAttribute configVar, IConfigVarContainer obj)
        {
            switch (obj)
            {
                case ConfigVarContainer<int> intField:
                    return SetupField(new IntegerField(), configVar, intField);
                case ConfigVarContainer<float> floatField:
                    return SetupField(new FloatField(), configVar, floatField);
                case ConfigVarContainer<bool> boolField:
                    return SetupField(new Toggle(), configVar, boolField);
                case ConfigVarStringContainer<FixedString32> stringField32:
                    return SetupTextField(new TextField(), configVar, stringField32);
                case ConfigVarStringContainer<FixedString64> stringField64:
                    return SetupTextField(new TextField(), configVar, stringField64);
                case ConfigVarStringContainer<FixedString128> stringField128:
                    return SetupTextField(new TextField(), configVar, stringField128);
                case ConfigVarStringContainer<FixedString512> stringField512:
                    return SetupTextField(new TextField(), configVar, stringField512);
                case ConfigVarStringContainer<FixedString4096> stringField4096:
                    return SetupTextField(new TextField(), configVar, stringField4096);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static BaseField<T> SetupField<T>(BaseField<T> field, ConfigVarAttribute configVar, ConfigVarContainer<T> container)
            where T : struct, IEquatable<T>
        {
            field.binding = new SharedStaticBinding<T>(field, container);

            field.label = configVar.Name;
            field.tooltip = configVar.Description;
            field.value = container.DirectValue;

            if (field is TextInputBaseField<T> textInputBaseField)
            {
                textInputBaseField.isReadOnly = configVar.IsReadOnly;
                textInputBaseField.isDelayed = true;
            }

            field.RegisterValueChangedCallback(evt =>
            {
                container.DirectValue = evt.newValue;
                PlayerPrefs.SetString(configVar.Name, container.DirectValue.ToString());
            });
            return field;
        }

        private static BaseField<string> SetupTextField<T>(TextInputBaseField<string> field, ConfigVarAttribute configVar, ConfigVarStringContainer<T> container)
            where T : struct
        {
            field.binding = new SharedStaticTextFieldBind<T>(field, container);

            field.label = configVar.Name;
            field.tooltip = configVar.Description;
            field.value = container.Value;
            field.isReadOnly = configVar.IsReadOnly;
            field.isDelayed = true;

            field.RegisterValueChangedCallback(evt =>
            {
                container.Value = evt.newValue;
                PlayerPrefs.SetString(configVar.Name, container.Value);
            });

            return field;
        }
    }
}