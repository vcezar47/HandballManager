using HandballManager.Mobile.Infrastructure;
using HandballManager.ViewModels;

namespace HandballManager.Mobile;

public partial class PlayerDetailPage : ContentPage
{
	public PlayerDetailPage(PlayerDetailViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;

		// Masked proxy: own/scouted players show exact values, others show estimation ranges.
		var m = vm.Masked;

		var technical = vm.Player.Position == "GK"
			? new (string, string)[]
			{
				("Reflexes", m.Reflexes), ("One on Ones", m.OneOnOnes),
				("Handling", m.Handling), ("Aerial Reach", m.AerialReach),
				("Communication", m.Communication), ("Eccentricity", m.Eccentricity),
				("Throwing", m.Throwing), ("Passing", m.Passing), ("Receiving", m.Receiving),
			}
			: new (string, string)[]
			{
				("Finishing", m.Finishing), ("Technique", m.Technique),
				("Dribbling", m.Dribbling), ("Passing", m.Passing),
				("Receiving", m.Receiving), ("Marking", m.Marking),
				("Tackling", m.Tackling), ("Long Throws", m.LongThrows),
				("7m Taking", m.SevenMeterTaking),
			};

		TechnicalHost.Content = AttributeGridBuilder.Build(technical);

		MentalHost.Content = AttributeGridBuilder.Build(new (string, string)[]
		{
			("Aggression", m.Aggression), ("Anticipation", m.Anticipation),
			("Composure", m.Composure), ("Concentration", m.Concentration),
			("Decisions", m.Decisions), ("Determination", m.Determination),
			("Flair", m.Flair), ("Leadership", m.Leadership),
			("Off the Ball", m.OffTheBall), ("Positioning", m.Positioning),
			("Teamwork", m.Teamwork), ("Vision", m.Vision),
		});

		PhysicalHost.Content = AttributeGridBuilder.Build(new (string, string)[]
		{
			("Acceleration", m.Acceleration), ("Agility", m.Agility),
			("Balance", m.Balance), ("Jumping Reach", m.JumpingReach),
			("Natural Fitness", m.NaturalFitness), ("Pace", m.Pace),
			("Stamina", m.Stamina), ("Strength", m.Strength),
		});
	}
}
