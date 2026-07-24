using System.Collections;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation.Abilities
{
    public sealed class MinefieldAbilityLogic : IAbilityLogic
    {
        private const float DeliveryDurationSeconds = 0.48f;
        private const float HyperDeliveryDurationSeconds = 0.40f;
        private const float DeliveryArcHeight = 1.55f;
        private const float DeliveryBundleRadius = 0.24f;
        private const float DeliveryFuseHeight = 0.16f;
        private const float LandingSplitPulseSeconds = 0.08f;

        private static Material _deliveryMaterial;
        private static Material _deliveryAccentMaterial;

        private readonly MinefieldAbilityDefinition _definition;

        public MinefieldAbilityLogic(MinefieldAbilityDefinition definition)
        {
            _definition = definition;
        }

        public AbilityExecutionResult Execute(IAbilityUser user, AbilityExecutionContext context)
        {
            if (_definition == null ||
                _definition.MineDefinition == null ||
                user is not BrawlerController owner)
            {
                return AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);
            }

            IDeployableService deployableService = ServiceProvider.Get<IDeployableService>();
            if (deployableService == null)
                return AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);

            Vector3 forward = ResolveForward(owner, context.Direction);
            Vector3 center = ResolveTargetPoint(context.Origin, forward, context);
            int count = Mathf.Max(1, _definition.MineCount);
            Vector3 origin = ResolveDeliveryOrigin(owner, context.Origin);
            owner.RunTimedBurst(DeliverMinefieldRoutine(
                owner,
                deployableService,
                context,
                origin,
                center,
                forward,
                count));

            AbilityExecutionResult result = AbilityExecutionResult.Succeeded(
                context.AbilityDefinition,
                context.SlotType);
            result.AppliedAreaEffect = true;
            result.TargetsAffected = count;
            result.ConsumedResource = true;
            return result;
        }

        public void Tick(uint currentTick) { }

        private IEnumerator DeliverMinefieldRoutine(
            BrawlerController owner,
            IDeployableService deployableService,
            AbilityExecutionContext context,
            Vector3 origin,
            Vector3 center,
            Vector3 forward,
            int count)
        {
            GameObject bundle = CreateDeliveryBundle(origin, forward, context.IsHypercharged);
            float duration = context.IsHypercharged
                ? HyperDeliveryDurationSeconds
                : DeliveryDurationSeconds;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (owner == null || !owner.gameObject.activeInHierarchy)
                {
                    DestroyDeliveryBundle(bundle);
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                Vector3 position = Vector3.Lerp(origin, center, eased);
                position.y += 4f * DeliveryArcHeight * t * (1f - t);

                if (bundle != null)
                {
                    bundle.transform.position = position;
                    Vector3 travelDirection = center - position;
                    travelDirection.y = 0f;
                    if (travelDirection.sqrMagnitude > 0.001f)
                        bundle.transform.rotation = Quaternion.LookRotation(travelDirection.normalized, Vector3.up);
                }

                yield return null;
            }

            DestroyDeliveryBundle(bundle);

            if (owner == null ||
                !owner.gameObject.activeInHierarchy ||
                deployableService == null)
            {
                yield break;
            }

            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 position = ResolveMinePosition(center, forward, i, count);
                DeployableSpawnRequest request = new DeployableSpawnRequest
                {
                    Owner = owner,
                    Team = owner.Team,
                    Definition = _definition.MineDefinition,
                    Position = position,
                    Direction = forward
                };

                if (deployableService.Spawn(request) != null)
                {
                    spawned++;
                    CreateLandingPulse(position, owner.Team, context.IsHypercharged);
                }
            }

            if (spawned <= 0)
                yield break;

            CombatPresentationEventBus.Raise(new CombatPresentationEvent
            {
                EventType = CombatPresentationEventType.AreaEffectResolved,
                Source = owner,
                Target = null,
                AbilityDefinition = context.AbilityDefinition,
                SlotType = context.SlotType,
                Position = center,
                Direction = forward,
                Value = spawned,
                IsSuper = true,
                IsHypercharged = context.IsHypercharged
            });
        }

        private Vector3 ResolveTargetPoint(
            Vector3 origin,
            Vector3 forward,
            AbilityExecutionContext context)
        {
            Vector3 target = context.HasTargetPoint
                ? context.TargetPoint
                : origin + forward * Mathf.Max(0.1f, _definition.Range);

            Vector3 offset = target - origin;
            offset.y = 0f;

            float range = Mathf.Max(0.1f, _definition.Range);
            if (offset.sqrMagnitude > range * range)
                offset = offset.normalized * range;

            Vector3 resolved = origin + offset;
            resolved.y = origin.y;
            return resolved;
        }

        private Vector3 ResolveMinePosition(Vector3 center, Vector3 forward, int index, int count)
        {
            if (count <= 1)
                return center;

            float spacing = Mathf.Max(0f, _definition.MineSpacing);
            float angle = count == 3
                ? 90f + index * 120f
                : index * 360f / count;
            Vector3 offset = Quaternion.AngleAxis(angle, Vector3.up) * forward * spacing;
            Vector3 position = center + offset;
            position.y = center.y;
            return position;
        }

        private static Vector3 ResolveDeliveryOrigin(BrawlerController owner, Vector3 fallbackOrigin)
        {
            Vector3 origin = owner != null
                ? owner.GetCastPosition()
                : fallbackOrigin;
            origin.y = fallbackOrigin.y + 0.55f;
            return origin;
        }

        private static GameObject CreateDeliveryBundle(
            Vector3 position,
            Vector3 forward,
            bool hypercharged)
        {
            GameObject root = new GameObject("BoMinefieldDeliveryBundle");
            root.transform.position = position;
            if (forward.sqrMagnitude > 0.001f)
                root.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);

            Color coreColor = hypercharged
                ? new Color(0.42f, 0.14f, 0.86f, 1f)
                : new Color(0.10f, 0.12f, 0.14f, 1f);
            Color accentColor = hypercharged
                ? new Color(0.86f, 0.48f, 1f, 1f)
                : new Color(1f, 0.68f, 0.18f, 1f);

            CreateBundlePart(
                root.transform,
                "MineBundleCore",
                PrimitiveType.Sphere,
                Vector3.zero,
                new Vector3(DeliveryBundleRadius, DeliveryBundleRadius * 0.78f, DeliveryBundleRadius),
                Quaternion.identity,
                coreColor,
                ResolveDeliveryMaterial());

            CreateBundlePart(
                root.transform,
                "MineBundleStripeA",
                PrimitiveType.Cylinder,
                new Vector3(0f, DeliveryFuseHeight, 0f),
                new Vector3(0.030f, 0.34f, 0.030f),
                Quaternion.Euler(90f, 0f, 0f),
                accentColor,
                ResolveDeliveryAccentMaterial());

            CreateBundlePart(
                root.transform,
                "MineBundleStripeB",
                PrimitiveType.Cylinder,
                new Vector3(0f, DeliveryFuseHeight * 0.55f, 0f),
                new Vector3(0.026f, 0.30f, 0.026f),
                Quaternion.Euler(90f, 90f, 0f),
                accentColor,
                ResolveDeliveryAccentMaterial());

            return root;
        }

        private static void CreateBundlePart(
            Transform parent,
            string name,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation,
            Color color,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation;
            part.transform.localScale = localScale;

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (material != null)
                    renderer.sharedMaterial = material;

                MaterialPropertyBlock block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_Color", color);
                block.SetColor("_BaseColor", color);
                block.SetColor("_EmissionColor", color);
                renderer.SetPropertyBlock(block);
            }
        }

        private static void CreateLandingPulse(Vector3 position, TeamType team, bool hypercharged)
        {
            GameObject pulse = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pulse.name = "BoMinefieldLandingPulse";
            pulse.transform.position = position + Vector3.up * 0.04f;
            pulse.transform.localScale = new Vector3(0.36f, 0.010f, 0.36f);

            Collider collider = pulse.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);

            Renderer renderer = pulse.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = ResolveDeliveryAccentMaterial();
                if (material != null)
                    renderer.sharedMaterial = material;

                Color color = hypercharged
                    ? new Color(0.76f, 0.34f, 1f, 0.80f)
                    : ResolveTeamColor(team);
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_Color", color);
                block.SetColor("_BaseColor", color);
                block.SetColor("_EmissionColor", color);
                renderer.SetPropertyBlock(block);
            }

            Object.Destroy(pulse, LandingSplitPulseSeconds);
        }

        private static Color ResolveTeamColor(TeamType team)
        {
            if (team == TeamType.Red)
                return new Color(1f, 0.24f, 0.18f, 0.78f);

            if (team == TeamType.Blue)
                return new Color(0.18f, 0.58f, 1f, 0.78f);

            return new Color(1f, 0.76f, 0.18f, 0.78f);
        }

        private static void DestroyDeliveryBundle(GameObject bundle)
        {
            if (bundle != null)
                Object.Destroy(bundle);
        }

        private static Material ResolveDeliveryMaterial()
        {
            if (_deliveryMaterial != null)
                return _deliveryMaterial;

            Shader shader =
                Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard") ??
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color");

            if (shader == null)
                return null;

            _deliveryMaterial = new Material(shader);
            return _deliveryMaterial;
        }

        private static Material ResolveDeliveryAccentMaterial()
        {
            if (_deliveryAccentMaterial != null)
                return _deliveryAccentMaterial;

            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Standard");

            if (shader == null)
                return null;

            _deliveryAccentMaterial = new Material(shader);
            return _deliveryAccentMaterial;
        }

        private static Vector3 ResolveForward(BrawlerController owner, Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
                return direction.normalized;

            Vector3 forward = owner.transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.001f
                ? forward.normalized
                : Vector3.forward;
        }
    }
}
