using System;
using T3.Core.Operator;
using T3.Core.Operator.Attributes;
using T3.Core.Operator.Slots;

namespace T3.Operators.Types.Id_5c7b6f3e_d3d5_4cfa_b30e_1a8cb6dbb4ad
{
    public class PairPointsForSplines : Instance<PairPointsForSplines>
    {

        [Output(Guid = "03404861-1a6f-413a-a3f8-b6316722f0c3")]
        public readonly Slot<T3.Core.DataTypes.BufferWithViews> OutBuffer = new Slot<T3.Core.DataTypes.BufferWithViews>();

        [Input(Guid = "d1a8aa7c-673a-4505-b614-b02742ea812f")]
        public readonly InputSlot<T3.Core.DataTypes.BufferWithViews> GPoints = new InputSlot<T3.Core.DataTypes.BufferWithViews>();

        [Input(Guid = "565c6d6e-6f80-4017-9ad8-4d7dddd667b6")]
        public readonly InputSlot<T3.Core.DataTypes.BufferWithViews> GTargets = new InputSlot<T3.Core.DataTypes.BufferWithViews>();

        [Input(Guid = "df3111f5-83fc-452d-8b54-3801070b049b")]
        public readonly InputSlot<bool> SetWTo01 = new InputSlot<bool>();

        [Input(Guid = "aee4be46-7857-4bba-861b-1b93421e0e45")]
        public readonly InputSlot<int> Segments = new InputSlot<int>();

        [Input(Guid = "5acb2824-a372-4c75-9896-c242fc753d6d")]
        public readonly InputSlot<System.Numerics.Vector3> TangentDirection = new InputSlot<System.Numerics.Vector3>();

        [Input(Guid = "0dd04da6-3b16-4aad-8421-78dc24d0dc68")]
        public readonly InputSlot<float> TangentA = new InputSlot<float>();

        [Input(Guid = "cc6594c9-b6e3-453a-8be0-e3305f2e1309")]
        public readonly InputSlot<float> TangentA_WFactor = new InputSlot<float>();

        [Input(Guid = "89f4ac50-b4b8-427c-9770-ef931ce91d1c")]
        public readonly InputSlot<float> TangentB = new InputSlot<float>();

        [Input(Guid = "b286781b-e808-40c3-8193-fbcc8215d534")]
        public readonly InputSlot<float> TangentB_WFactor = new InputSlot<float>();
    }
}

