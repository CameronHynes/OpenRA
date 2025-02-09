﻿#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Renders a parachute on units.")]
	public class WithParachuteInfo : UpgradableTraitInfo, ITraitInfo, IRenderActorPreviewSpritesInfo, Requires<RenderSpritesInfo>, Requires<BodyOrientationInfo>
	{
		[Desc("The image that contains the parachute sequences.")]
		public readonly string Image = null;

		[Desc("Parachute opening sequence.")]
		[SequenceReference("Image")] public readonly string OpeningSequence = null;

		[Desc("Parachute idle sequence.")]
		[SequenceReference("Image")] public readonly string Sequence = null;

		[Desc("Parachute closing sequence. Defaults to opening sequence played backwards.")]
		[SequenceReference("Image")] public readonly string ClosingSequence = null;

		[Desc("Palette used to render the parachute.")]
		public readonly string Palette = "player";

		[Desc("Parachute position relative to the paradropped unit.")]
		public readonly WVec Offset = new WVec(0, 0, 384);

		[Desc("The image that contains the shadow sequence for the paradropped unit.")]
		public readonly string ShadowImage = null;

		[Desc("Paradropped unit's shadow sequence.")]
		[SequenceReference("ShadowImage")] public readonly string ShadowSequence = null;

		[Desc("Palette used to render the paradropped unit's shadow.")]
		public readonly string ShadowPalette = "shadow";

		[Desc("Shadow position relative to the paradropped unit's intended landing position.")]
		public readonly WVec ShadowOffset = new WVec(0, 128, 0);

		[Desc("Z-offset to apply on the shadow sequence.")]
		public readonly int ShadowZOffset = 0;

		public override object Create(ActorInitializer init) { return new WithParachute(init.Self, this); }

		public IEnumerable<IActorPreview> RenderPreviewSprites(ActorPreviewInitializer init, RenderSpritesInfo rs, string image, int facings, PaletteReference p)
		{
			if (UpgradeMinEnabledLevel > 0)
				yield break;

			if (image == null)
				yield break;

			// For this, image must not be null
			if (Palette != null)
				p = init.WorldRenderer.Palette(Palette);

			var anim = new Animation(init.World, image);
			anim.PlayThen(OpeningSequence, () => anim.PlayRepeating(Sequence));

			var body = init.Actor.TraitInfo<BodyOrientationInfo>();
			var facing = init.Contains<FacingInit>() ? init.Get<FacingInit, int>() : 0;
			var orientation = body.QuantizeOrientation(new WRot(WAngle.Zero, WAngle.Zero, WAngle.FromFacing(facing)), facings);
			var offset = body.LocalToWorld(Offset.Rotate(orientation));
			yield return new SpriteActorPreview(anim, offset, offset.Y + offset.Z + 1, p, rs.Scale);
		}
	}

	public class WithParachute : UpgradableTrait<WithParachuteInfo>, IRender
	{
		readonly Animation shadow;
		readonly AnimationWithOffset anim;
		readonly WithParachuteInfo info;

		bool renderProlonged = false;

		public WithParachute(Actor self, WithParachuteInfo info)
			: base(info)
		{
			this.info = info;

			if (info.ShadowImage != null)
			{
				shadow = new Animation(self.World, info.ShadowImage);
				shadow.PlayRepeating(info.ShadowSequence);
			}

			if (info.Image == null)
				return;

			// For this, info.Image must not be null
			var overlay = new Animation(self.World, info.Image);
			var body = self.Trait<BodyOrientation>();
			anim = new AnimationWithOffset(overlay,
				() => body.LocalToWorld(info.Offset.Rotate(body.QuantizeOrientation(self, self.Orientation))),
				() => IsTraitDisabled && !renderProlonged,
				p => RenderUtils.ZOffsetFromCenter(self, p, 1));

			var rs = self.Trait<RenderSprites>();
			rs.Add(anim, info.Palette);
		}

		protected override void UpgradeEnabled(Actor self)
		{
			if (info.Image == null)
				return;

			anim.Animation.PlayThen(info.OpeningSequence, () => anim.Animation.PlayRepeating(info.Sequence));
		}

		protected override void UpgradeDisabled(Actor self)
		{
			if (info.Image == null)
				return;

			renderProlonged = true;
			if (!string.IsNullOrEmpty(info.ClosingSequence))
				anim.Animation.PlayThen(info.ClosingSequence, () => renderProlonged = false);
			else
				anim.Animation.PlayBackwardsThen(info.OpeningSequence, () => renderProlonged = false);
		}

		public IEnumerable<IRenderable> Render(Actor self, WorldRenderer wr)
		{
			if (info.ShadowImage == null)
				yield break;

			if (IsTraitDisabled)
				yield break;

			if (self.IsDead || !self.IsInWorld)
				yield break;

			if (self.World.FogObscures(self))
				yield break;

			shadow.Tick();
			var pos = self.CenterPosition - new WVec(0, 0, self.CenterPosition.Z);
			yield return new SpriteRenderable(shadow.Image, pos, info.ShadowOffset, info.ShadowZOffset, wr.Palette(info.ShadowPalette), 1, true);
		}
	}
}
