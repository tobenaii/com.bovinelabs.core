﻿// <copyright file="InputCommonSettings.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

#if !BL_DISABLE_INPUT
namespace BovineLabs.Core.Authoring.Input
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using BovineLabs.Core.Authoring.Settings;
    using BovineLabs.Core.Input;
    using Unity.Entities;
    using UnityEngine;
    using UnityEngine.InputSystem;

    public class InputCommonSettings : SettingsBase
    {
        [SerializeField]
        private InputActionAsset? asset;

        [SerializeField]
        private InputActionReference? cursorPosition;

        [SerializeField]
        [SerializeReference]
        [SuppressMessage("ReSharper", "CollectionNeverUpdated.Local", Justification = "Unity serialization")]
        [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local", Justification = "Unity serialization")]
        private List<IInputSettings> settings = new();

        /// <inheritdoc />
        public override void Bake(IBaker baker)
        {
            var entity = baker.GetEntity(TransformUsageFlags.None);

            var defaultSettings = new InputDefault
            {
                Asset = this.asset!,
                CursorPosition = baker.DependsOn(this.cursorPosition)!,
            };

            baker.AddComponent(entity, defaultSettings);
            baker.AddComponent<InputCommon>(entity);

            var wrapper = new BakerWrapper(baker, entity);

            foreach (var s in this.settings)
            {
                s?.Bake(wrapper);
            }
        }

        private class BakerWrapper : IBakerWrapper
        {
            private readonly IBaker baker;
            private readonly Entity entity;

            public BakerWrapper(IBaker baker, Entity entity)
            {
                this.baker = baker;
                this.entity = entity;
            }

            public void AddComponent<T>(T component)
                where T : unmanaged, IComponentData
            {
                this.baker.AddComponent(this.entity, component);
            }

            public T DependsOn<T>(T obj)
                where T : Object
            {
                return this.baker.DependsOn(obj);
            }
        }
    }
}
#endif