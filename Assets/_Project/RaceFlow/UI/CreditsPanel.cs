using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // Task P7: the two credit pages (team, then tools and sources). The texts live in RaceHud.uxml.
    // A/D, the arrow keys, Space or the NEXT PAGE button turn the page; the dots jump to a page.
    // Plain C#: HudMenus creates it and calls HandleKeys() every frame while the credits are open.
    public class CreditsPanel
    {
        private readonly VisualElement[] pages;
        private readonly VisualElement[] dots;
        private readonly VisualElement nextButton;
        private int page;

        public CreditsPanel(VisualElement card)
        {
            pages = new VisualElement[] { card.Q<VisualElement>("credits-page-1"), card.Q<VisualElement>("credits-page-2") };
            dots = new VisualElement[] { card.Q<VisualElement>("credits-dot-1"), card.Q<VisualElement>("credits-dot-2") };
            nextButton = card.Q<VisualElement>("credits-next");
            nextButton.RegisterCallback<ClickEvent>(evt => Turn(1));
            for (int i = 0; i < dots.Length; i++)
            {
                int target = i; // a copy per dot, so each dot opens its own page
                dots[i].RegisterCallback<ClickEvent>(evt => ShowPage(target));
            }
            ShowPage(0);
        }

        // Always start on the team page.
        public void Open()
        {
            ShowPage(0);
        }

        public void HandleKeys()
        {
            int horizontal = RaceKeys.MenuHorizontal();
            if (horizontal != 0)
            {
                Turn(horizontal);
            }
            else if (RaceKeys.MenuTogglePressed())
            {
                Turn(1);
            }
        }

        public void SetClickable(bool clickable)
        {
            HudFx.SetClickable(nextButton, clickable);
            for (int i = 0; i < dots.Length; i++)
            {
                HudFx.SetClickable(dots[i], clickable);
            }
        }

        private void Turn(int direction)
        {
            ShowPage(MenuNavigation.Wrap(page, direction, pages.Length));
        }

        // The shown page fades in (USS "is-shown"), the dot of that page turns sand.
        private void ShowPage(int index)
        {
            page = index;
            for (int i = 0; i < pages.Length; i++)
            {
                HudFx.SetShown(pages[i], i == page);
                dots[i].EnableInClassList("credits-dot--on", i == page);
            }
        }
    }
}
