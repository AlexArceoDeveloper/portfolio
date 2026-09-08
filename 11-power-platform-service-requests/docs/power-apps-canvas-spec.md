# Canvas app specification

## Screens

| Screen | Responsibility | Primary controls |
| --- | --- | --- |
| `HomeScreen` | Entry point and status summary. | New request button, requester-scoped gallery. |
| `RequestFormScreen` | Creates or edits a request. | Category, summary, description, impact, submit button. |
| `RequestDetailScreen` | Shows current state and history. | Status badge, owner, timeline, resolution field for authorised owners. |

## SharePoint data source

Add the `Service Requests` list as `ServiceRequests`. The app should filter the requester gallery according to the organisation's identity and access policy; this sample uses the creator email for illustration.

```powerfx
SortByColumns(
    Filter(ServiceRequests, RequesterEmail = User().Email),
    "Modified",
    SortOrder.Descending
)
```

## Submit behaviour

The submit button validates on the client before creating the list item. The Power Automate flow repeats business validation once the item is created.

```powerfx
If(
    IsBlank(CategoryDropdown.Selected.Value) ||
    IsBlank(Trim(SummaryInput.Text)) ||
    IsBlank(ImpactRadio.Selected.Value),
    Notify("Choose a category, add a summary and select an impact level.", NotificationType.Error),
    IfError(
        Patch(
            ServiceRequests,
            Defaults(ServiceRequests),
            {
                Title: Left(Trim(SummaryInput.Text), 120),
                Category: { Value: CategoryDropdown.Selected.Value },
                Description: Trim(DescriptionInput.Text),
                Impact: { Value: ImpactRadio.Selected.Value },
                RequesterEmail: User().Email,
                Status: { Value: "New" }
            }
        ),
        Notify("The request could not be saved. Please try again.", NotificationType.Error),
        Notify("Your request was submitted.", NotificationType.Success);
        Navigate(HomeScreen, ScreenTransition.Fade)
    )
)
```

## Accessibility and usability

- Every input has a visible label and accessible label.
- Do not rely on colour alone for priority or status; include clear text.
- Error messages explain the corrective action.
- The request form is keyboard reachable and usable at narrow viewport widths.
- Limit free-text fields to the data needed for the workflow. Avoid sensitive personal data in the sample scenario.

## Copilot Studio handoff

The optional topic helps a requester select a category and formulate a concise summary. It must hand off to `RequestFormScreen` with suggested values only. The requester confirms all information in the Canvas app before a list item is created.

