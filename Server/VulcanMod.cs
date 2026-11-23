using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Modding.Custom;

namespace Hawks.Arges;

[Injectable(TypePriority = OnLoadOrder.Preload + 1)]
public sealed class ArgesMod(CustomItemService customItems, TemplateTable templates, GlobalTable globals,
    BotTable bots) : IOnLoad
{
    public const string WeaponId = "686a13500000000000000001";
    public const string FeedId = "686a13500000000000000002";
    private const string Donor = "661ceb1b9311543c7104149b";
    private const string DonorFeed = "660ea4453786cc0af808a1be";
    private const string ShortName = "Arges";

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        Clone(DonorFeed, FeedId, "Arges ammunition belt", "Arges 弹链", "Arges 弾帯", 12000);
        Clone(Donor, WeaponId, "M135 Arges 7.62x51 minigun", "M135 Arges 7.62x51 转管机枪", "M135 Arges 7.62x51 ミニガン", 250000);
        var weapon = templates.Items[WeaponId].Properties!;
        weapon.Prefab = new Prefab { Path = "hawks/weapon_m135_arges.bundle", Rcid = "" };
        templates.Items[FeedId].Properties!.Prefab = new Prefab { Path = "hawks/mag_m135_arges.bundle", Rcid = "" };
        var feedSlot = weapon.Slots!.Single(slot => slot.Name == "mod_magazine");
        foreach (var filter in feedSlot.Properties!.Filters!)
            filter.Filter = [FeedId];
        weapon.Slots = [feedSlot];
        weapon.DefMagType = FeedId;
        weapon.CanSellOnRagfair = true;
        templates.Items[FeedId].Properties!.CanSellOnRagfair = true;
        weapon.BFirerate = 1337;
        weapon.WeapFireType = ["fullauto"];
        const string presetId = "686a13500000000000000031";
        const string presetRoot = "686a13500000000000000032";
        globals.ItemPresets[presetId] = new Preset
        {
            Id = presetId, Type = "Preset", Encyclopedia = WeaponId, Parent = presetRoot,
            Name = ShortName, ChangeWeaponName = false,
            Items = [new Item { Id = presetRoot, Template = WeaponId }, FeedItem("686a13500000000000000033", presetRoot)]
        };


        foreach (var bot in bots.Types.Values)
        {
            if (bot?.BotInventory is not { Equipment: { } equipment } inventory)
                continue;
            bool eligible = false;
            foreach (var pool in equipment.Values)
            {
                if (!pool.TryGetValue(Donor, out var weight))
                    continue;
                pool[WeaponId] = weight;
                eligible = true;
            }

            if (!eligible)
                continue;
            inventory.Mods[WeaponId] = new() { ["mod_magazine"] = [FeedId] };
            if (inventory.Mods.TryGetValue(DonorFeed, out var feedMods))
                inventory.Mods[FeedId] = feedMods;
        }
        return Task.CompletedTask;
    }

    private void Clone(string donorId, string id, string en, string zh, string ja, double price)
    {
        var handbook = templates.Handbook.Items.Single(item => item.Id == donorId);
        var locales = new Dictionary<string, LocaleDetails>();
        foreach (var (locale, name) in new[] { ("en", en), ("ch", zh), ("ja", ja) })
            locales[locale] = new LocaleDetails { Name = name, ShortName = ShortName, Description = id == WeaponId ? locale switch { "en" => "Isn’t it weird that they call this thing a minigun? There’s nothing “mini” about it! It’s huge, its bullets are huge, and it can do a huge amount of damage. I guess “huge-gun” doesn’t have the same ring to it. Big-gun, maybe? Whopper-gun? Eh, never mind.", "ch" => "他们管这玩意儿叫迷你枪，是不是很奇怪？它根本不“迷你”！枪身和子弹都很大，威力也一点都不小。我觉得“巨型枪”就更合适，一听就懂。或者叫超大枪？巨无霸枪？呃，还是算了，别想了。", "ja" => "どうしてこいつのことを「ミニガン」なんて言うんだろうな。本体もデカけりゃ弾もデカいし、ダメージだってデカいのに。きっと、「ヒュージガン」って響きがあんまりイケてないんだろうな。ビッグガンとか、ドデカガンとかならどうだ？ ま、どうでもいいか", _ => name } : name };

        var result = customItems.CreateItemFromClone(new()
        {
            ItemTplToClone = donorId, NewId = id, NewItemName = "hawks_arges_" + id[^2..],
            ParentId = templates.Items[donorId].Parent, HandbookParentId = handbook.ParentId,
            HandbookPriceRoubles = price, FleaPriceRoubles = price, Locales = locales
        });
        if (!result.Success)
            throw new InvalidOperationException(string.Join("; ", result.Errors));
    }

    private static Item FeedItem(string id, string parent) =>
        new() { Id = id, ParentId = parent, SlotId = "mod_magazine", Template = FeedId };

}


[Injectable(TypePriority = OnLoadOrder.PostLoad + 1000)]
public sealed class ArgesInventoryDimensions(TemplateTable templates) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var weapon = templates.Items[ArgesMod.WeaponId].Properties!;
        weapon.Weight = 19.35;
        weapon.HipInnaccuracyGain = 0;
        weapon.Ergonomics = 0;
        // Factory SA-58: 280 mm barrel (0.08), +2 and +3 accuracy from its fitted parts.
        weapon.CenterOfImpact = 0.08 * 0.95 - 0.12 * 2.9089 / 100;
        weapon.RecoilForceUp = 128;
        weapon.RecoilAngle = 90;
        weapon.HipAccuracyRestorationDelay = 0.12;
        weapon.HipAccuracyRestorationSpeed = 10;
        weapon.Width = 5;
        weapon.Height = 2;
        var feed=templates.Items[ArgesMod.FeedId].Properties!;
        feed.ExtraSizeDown=1;
        feed.Accuracy=0;
        feed.Ergonomics=0;
        var cartridgeSlot=feed.Cartridges!.Single();
        cartridgeSlot.Id="686a13500000000000000004";
        cartridgeSlot.Parent=ArgesMod.FeedId;
        cartridgeSlot.MaxCount=500;
        return Task.CompletedTask;
    }
}
