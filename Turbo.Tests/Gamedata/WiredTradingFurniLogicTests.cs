using FluentAssertions;
using Turbo.Gamedata.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Xunit;

namespace Turbo.Tests.Gamedata;

/// <summary>
/// The wired trading furni get the server logic that opens their window: the chests by the client
/// logic their asset file names (furniture_furnichest, furniture_coinschest), the contracts, plain
/// furniture to the client, by their class. On plain furniture a double-click only switched the
/// chest's state.
/// </summary>
public sealed class WiredTradingFurniLogicTests
{
    private static readonly HashSet<string> LOGICS =
    [
        "default_floor",
        "wired_chest_furni",
        "wired_chest_coins",
        "wired_contract_trade",
        "wired_contract_payment",
        "wired_contract_reward",
    ];

    [Theory]
    [InlineData("wf_storage_furni1", "furniture_furnichest", "wired_chest_furni")]
    [InlineData("wf_storage_furni_starter", "furniture_furnichest", "wired_chest_furni")]
    [InlineData("wf_storage_coins2", "furniture_coinschest", "wired_chest_coins")]
    [InlineData("wf_contract_trade", "furniture_basic", "wired_contract_trade")]
    [InlineData("wf_contract_payment", "furniture_basic", "wired_contract_payment")]
    [InlineData("wf_contract_reward", "furniture_basic", "wired_contract_reward")]
    [InlineData("chair_norja", "furniture_basic", "default_floor")]
    public void The_import_gives_wired_trading_furni_their_logic(
        string className,
        string clientLogic,
        string logic
    ) =>
        FurnitureDrafts
            .LogicOf(ProductType.Floor, className, clientLogic, clientCategory: "wired", LOGICS)
            .Should()
            .Be(logic);
}
