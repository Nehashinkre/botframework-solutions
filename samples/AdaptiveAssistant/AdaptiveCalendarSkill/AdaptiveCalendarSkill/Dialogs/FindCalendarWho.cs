﻿using AdaptiveCalendarSkill.Services;
using Luis;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Adaptive;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Actions;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Events;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Input;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Builder.LanguageGeneration;
using System.Collections.Generic;

/// <summary>
/// This dialog will show all the calendar entries if they have the same email address
/// </summary>
namespace AdaptiveCalendarSkill.Dialogs
{
    public class FindCalendarWho : ComponentDialog
    {
        public FindCalendarWho(
            BotSettings settings,
            BotServices services,
            OAuthPromptDialog oauthDialog)
            : base(nameof(FindCalendarEntry))
        {
            var adaptiveDialog = new AdaptiveDialog("find")
            {
                Recognizer = services.CognitiveModelSets["en"].LuisServices["Calendar"],
                Generator = new ResourceMultiLanguageGenerator("FindCalendarWho.lg"),
                Events =
                {
                    new OnBeginDialog()
                    {
                        Actions =
                        {
                            new BeginDialog(oauthDialog.Id),
                            // get our calendar
                            new HttpRequest(){
                                Url = "https://graph.microsoft.com/v1.0/me/calendarview?startdatetime={utcNow()}&enddatetime={addDays(utcNow(), 1)}",
                                Method = HttpRequest.HttpMethod.GET,
                                Headers =  new Dictionary<string, string>()
                                {
                                    ["Authorization"] = "Bearer {user.token.token}",
                                },
                                Property = "dialog.FindCalendarWho_GraphAll"
                            },
                            // Handle case where there are no items in calendar
                            new IfCondition()
                            {
                                Condition = "dialog.FindCalendarWho_GraphAll.value == null || count(dialog.FindCalendarWho_GraphAll.value) <= 0",
                                Actions =
                                {
                                    new SendActivity("[ViewEmptyList]"),
                                    new SendActivity("[Welcome-Actions]"),
                                    new EndDialog()
                                }
                            },
                            // show our non-empty calendar
                            new TextInput()
                            {
                                Property = "dialog.findCalendarWho_entryName",
                                Prompt = new ActivityTemplate("[GetPersonName]"),
                            },
                            // to iterate all the entries to find all matches
                            new Foreach()
                            {
                                ListProperty = "dialog.FindCalendarWho_GraphAll.value",
                                Actions =
                                {
                                    new IfCondition()
                                    {
                                        Condition = "dialog.FindCalendarWho_GraphAll.value[dialog.index].attendees[0] != null",
                                        Actions ={
                                            new IfCondition(){
                                                Condition = "contains(dialog.FindCalendarWho_GraphAll.value[dialog.index].attendees[0].emailAddress.address, " +
                                                    "dialog.findCalendarWho_entryName) == true || " +
                                                    "contains(dialog.FindCalendarWho_GraphAll.value[dialog.index].attendees[0].emailAddress.name, " +
                                                    "dialog.findCalendarWho_entryName) == true",
                                                Actions ={
                                                    new EditArray(){
                                                        ArrayProperty = "dialog.findCalendarWho_found",
                                                        ChangeType = EditArray.ArrayChangeType.Push,
                                                        Value = "dialog.FindCalendarWho_GraphAll.value[dialog.index]"
                                                    },
                                                }
                                            }
                                        },

                                    }
                                }
                            },
                            // no matches situation
                            new IfCondition()
                            {
                                Condition = "dialog.findCalendarWho_found == null || count(dialog.findCalendarWho_found) <= 0",
                                Actions ={
                                    new SendActivity("[NoMatches]"),
                                    new SendActivity("[Welcome-Actions]"),
                                    new EndDialog()
                                }
                            },
                            // matched found
                            new SendActivity("[entryTemplate(dialog.findCalendarWho_found[user.FindCalendarWho_pageIndex * 3], " +
                                        "dialog.findCalendarWho_found[user.FindCalendarWho_pageIndex * 3 + 1]," +
                                        "dialog.findCalendarWho_found[user.FindCalendarWho_pageIndex * 3 + 2])]"),// TODO only simple card right now, will use fancy card then\
                            new DeleteProperty
                            {
                                Property = "turn.FindCalendarWho_Choice"
                            },
                            new ChoiceInput(){
                                Property = "turn.FindCalendarWho_Choice",
                                Prompt = new ActivityTemplate("[EnterYourChoice]"),
                                Choices = new List<Choice>()
                                {
                                    new Choice("Check The First One"),
                                    new Choice("Check The Second One"),
                                    new Choice("Check The Third One"),
                                    new Choice("Next Page"),
                                    new Choice("Previous Page")
                                },
                                Style = ListStyle.SuggestedAction
                            },
                            new SwitchCondition()
                            {
                                Condition = "turn.FindCalendarWho_Choice",
                                Cases = new List<Case>()
                                {
                                    new Case("Check The First One", new List<IDialog>()
                                    {
                                        new IfCondition(){
                                            Condition = "dialog.findCalendarWho_found[user.FindCalendarWho_pageIndex * 3] != null",
                                            Actions ={
                                                new SendActivity("[detailedEntryTemplate(dialog.findCalendarWho_found[user.FindCalendarWho_pageIndex * 3])]"),
                                                new SetProperty()
                                                {
                                                    Property = "user.focusedMeeting",
                                                    Value = "dialog.findCalendarWho_found[user.FindCalendarWho_pageIndex * 3]"
                                                }
                                            },
                                            ElseActions ={
                                                new SendActivity("[viewEmptyEntry]")
                                            }
                                        },
                                        //new RepeatDialog()
                                        // otherwise, once we change to other intents, we will still come back
                                        }),
                                    new Case("Check The Second One", new List<IDialog>()
                                        {
                                            new IfCondition(){
                                                Condition = "dialog.findCalendarWho_found[user.FindCalendarWho_pageIndex * 3 + 1] != null",
                                                Actions ={
                                                    new SendActivity("[detailedEntryTemplate(dialog.findCalendarWho_found[user.FindCalendarWho_pageIndex * 3 + 1])]"),
                                                    new SetProperty()
                                                    {
                                                        Property = "user.focusedMeeting",
                                                        Value = "dialog.findCalendarWho_found[user.FindCalendarWho_pageIndex * 3 + 1]"
                                                    }
                                                },
                                                ElseActions ={
                                                    new SendActivity("[viewEmptyEntry]")
                                                }
                                            },
                                            //new RepeatDialog()
                                        }),
                                    new Case("Check The Third One", new List<IDialog>()
                                        {
                                            new IfCondition(){
                                                Condition = "dialog.findCalendarWho_found[user.FindCalendarWho_pageIndex * 3 + 2] != null",
                                                Actions ={
                                                    new SendActivity("[detailedEntryTemplate(dialog.findCalendarWho_found[user.FindCalendarWho_pageIndex * 3 + 2])]"),
                                                    new SetProperty()
                                                    {
                                                        Property = "user.focusedMeeting",
                                                        Value = "dialog.findCalendarWho_found[user.FindCalendarWho_pageIndex * 3 + 2]"
                                                    }
                                                },
                                                ElseActions ={
                                                    new SendActivity("[viewEmptyEntry]")
                                                }
                                            },
                                            //new RepeatDialog()
                                        }),
                                    new Case("Next Page", new List<IDialog>()
                                        {
                                            new IfCondition()
                                            {
                                                Condition = "user.FindCalendarWho_pageIndex * 3 < count(dialog.findCalendarWho_found.value)",
                                                Actions =
                                                {
                                                    new SetProperty()
                                                    {
                                                        Property = "user.FindCalendarWho_pageIndex",
                                                        Value = "user.FindCalendarWho_pageIndex + 1"
                                                    },
                                                    new RepeatDialog()
                                                },
                                                ElseActions =
                                                {
                                                    new SendActivity("This is already the last page!"),
                                                    new RepeatDialog()
                                                }
                                            }
                                        }),
                                    new Case("Previous Page", new List<IDialog>()
                                        {
                                            new IfCondition()
                                            {
                                                Condition = " 0 < user.FindCalendarWho_pageIndex",
                                                Actions =
                                                {
                                                    new SetProperty()
                                                    {
                                                        Property = "user.FindCalendarWho_pageIndex",
                                                        Value = "user.FindCalendarWho_pageIndex - 1"
                                                    },
                                                    new RepeatDialog()
                                                },
                                                ElseActions =
                                                {
                                                    new SendActivity("This is already the first page!"),
                                                    new RepeatDialog()
                                                }
                                            }
                                        }),
                                },
                                Default =
                                {
                                    new SendActivity("[NoMeans]"),
                                    new EndDialog()
                                }
                            },
                            new SendActivity("[Welcome-Actions]"),
                            new EndDialog()
                        }
                    },
                    new OnIntent(GeneralLuis.Intent.Help.ToString())
                    {
                        Actions = { new SendActivity("[HelpFindMeeting]") }
                    },
                    new OnIntent(GeneralLuis.Intent.Cancel.ToString())
                    {
                        Actions = {
                            new SendActivity("[CancelFindMeeting]"),
                            new CancelAllDialogs()
                        }
                    }
                }
            };

            // Add named dialogs to the DialogSet. These names are saved in the dialog state.
            AddDialog(adaptiveDialog);
            AddDialog(oauthDialog);

            // The initial child Dialog to run.
            InitialDialogId = adaptiveDialog.Id;
        }
    }
}