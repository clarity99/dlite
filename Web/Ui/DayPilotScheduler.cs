/*
Copyright © 2005 - 2008 Annpoint, s.r.o.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

-------------------------------------------------------------------------

NOTE: Reuse requires the following acknowledgement (see also NOTICE):
This product includes DayPilot (http://www.daypilot.org) developed by Annpoint, s.r.o.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Security.Permissions;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using DayPilot.Utils;

namespace DayPilot.Web.Ui
{
    /// <summary>
    /// Calendar/scheduler control with hours on the horizontal axis and resources on the vertical axis.
    /// </summary>
    /// <remarks>
    /// </remarks>
    [PersistChildren(false)]
    [ParseChildren(true)]
    [DefaultProperty(null)]
    [ToolboxBitmap(typeof(Calendar))]
    [AspNetHostingPermission(SecurityAction.LinkDemand, Level = AspNetHostingPermissionLevel.Minimal), AspNetHostingPermission(SecurityAction.InheritanceDemand, Level = AspNetHostingPermissionLevel.Minimal)]
    public class DayPilotScheduler : DataBoundControl, IPostBackEventHandler
    {
        internal List<Day> days;

        private string dataStartField;
        private string dataEndField;
        private string dataTextField;
        private string dataValueField;
        private string dataResourceField;


        /// <summary>
        /// Event called when the user clicks an event in the calendar. It's only called when EventClickHandling is set to PostBack.
        /// </summary>
        [Category("User actions")]
        [Description("Event called when the user clicks an event in the calendar.")]
        public event EventClickDelegate EventClick;

        /// <summary>
        /// Event called when the user clicks a free space in the calendar. It's only called when FreeTimeClickHandling is set to PostBack.
        /// </summary>
        [Category("User actions")]
        [Description("Event called when the user clicks a free space in the calendar.")]
        public event FreeClickDelegate FreeTimeClick;


        /// <summary>
        /// Renders the component HTML code.
        /// </summary>
        /// <param name="output"></param>
        protected override void Render(HtmlTextWriter output)
        {
            loadEventsToDays();
            renderMainTable(output);
        }


        private void renderMainTable(HtmlTextWriter output)
        {
            output.AddAttribute("id", ClientID);
            output.AddStyleAttribute("width", Width + "px");
            output.AddStyleAttribute("line-height", "1.2");
            output.RenderBeginTag("div");

            output.AddAttribute("cellspacing", "0");
            output.AddAttribute("cellpadding", "0");
            output.AddAttribute("border", "0");
            output.AddStyleAttribute("background-color", ColorTranslator.ToHtml(HourNameBackColor));
            output.RenderBeginTag("table");

            output.RenderBeginTag("tr");
            output.AddStyleAttribute("width", (RowHeaderWidth) + "px");
            output.RenderBeginTag("td");
            renderCorner(output);
            output.RenderEndTag(); // td
            renderHeaderCols(output);
            output.RenderEndTag(); // tr

            // SOUTH
            renderRows(output);
            
            output.RenderEndTag(); // table

            output.RenderEndTag(); // main
        }

        private void renderCells(HtmlTextWriter output, Day d)
        {
            int cellsToRender = cellCount;

            for (int i = 0; i < cellsToRender; i++)
            {
                DateTime start = d.Start.AddMinutes(i*CellDuration);

                int thisCellWidth = CellWidth;
                if (i == 0)
                {
                    thisCellWidth = CellWidth - 1;
                }
                string back = getCellColor(start, d.Value);

                if (i == cellsToRender - 1)
                {
                    output.AddStyleAttribute("border-right", "1px solid " + ColorTranslator.ToHtml(BorderColor));
                }
                output.AddStyleAttribute("border-bottom", "1px solid " + ColorTranslator.ToHtml(BorderColor));
                output.AddStyleAttribute("width", thisCellWidth + "px");
                output.AddStyleAttribute("background-color", back);
                output.AddStyleAttribute("cursor", "pointer");

                if (FreeTimeClickHandling == UserActionHandling.PostBack)
                {
                    output.AddAttribute("onclick", "javascript:" + Page.ClientScript.GetPostBackEventReference(this, "TIME:" + start.ToString("s") + d.Value));
                }
                else
                {
                    output.AddAttribute("onclick", "javascript:" + String.Format(FreeTimeClickJavaScript, start.ToString("s"), d.Value));
                }
                output.AddAttribute("onmouseover", "this.style.backgroundColor='" + ColorTranslator.ToHtml(HoverColor) + "';");
                output.AddAttribute("onmouseout", "this.style.backgroundColor='" + back + "';");

                output.RenderBeginTag("td");

                output.Write("<div unselectable='on' style='display:block; width:" + (thisCellWidth - 1) + "px; height:" + (d.MaxColumns() * EventHeight - 1) + "px; border-right: 1px solid " + ColorTranslator.ToHtml(HourBorderColor) + ";' ><!-- --></div>");
                output.RenderEndTag();

            }
        }

        private void renderEvents(Day d, HtmlTextWriter output)
        {
            if (d.events.Count == 0)
            {
                output.Write("<div style='height:" + (EventHeight - 1) + "px;position:relative;width:1px;overflow:none;' unselectable='on'><!-- --></div>");
            }
            else
            {
                output.AddStyleAttribute("position", "relative");
                output.AddStyleAttribute("height", (d.MaxColumns() * EventHeight - 1) + "px"); //
                output.AddStyleAttribute("overflow", "none");
                output.AddAttribute("unselectable", "on");
                output.RenderBeginTag("div");

                foreach (Event ep in d.events)
                {
                    renderEvent(d, ep, output);
                }

                // div relative
                output.RenderEndTag();
            }
        }


        private void renderEvent(Day d, Event p, HtmlTextWriter output)
        {
            int max = cellCount*CellWidth;

            int left = (int)Math.Floor((p.BoxStart - d.Start).TotalMinutes * CellWidth / CellDuration);
            int top = p.Column.Number * EventHeight - 1;
            int width = (int)Math.Floor((p.BoxEnd - p.BoxStart).TotalMinutes * CellWidth / CellDuration) - 2;
            int height = EventHeight - 1;

            int startDelta = (int)Math.Floor((p.Start - p.BoxStart).TotalMinutes * CellWidth / CellDuration - 1);
            int realWidth = (int)Math.Floor((p.End - p.Start).TotalMinutes * CellWidth / CellDuration);
            realWidth = realWidth == 0 ? 1 : realWidth;


            // adjustments
            if (left > max) // don't render
            {
                return;
            }
            if (left + width > max - 2)
            {
                width = max - left - 2;
            }
            if (left < 0)
            {
                width += left;
                left = 0;
            }

            width = Math.Max(width, 2);
            output.AddAttribute("unselectable", "on");

            output.AddStyleAttribute("position", "absolute");
            output.AddStyleAttribute("left", left + "px");
            output.AddStyleAttribute("top", top + "px");
            output.AddStyleAttribute("width", width + "px");
            output.AddStyleAttribute("height", height + "px");
            output.AddStyleAttribute("border", "1px solid " + ColorTranslator.ToHtml(EventBorderColor));
            output.AddStyleAttribute("background-color", ColorTranslator.ToHtml(EventBackColor));
            output.AddStyleAttribute("white-space", "nowrap");
            output.AddStyleAttribute("overflow", "hidden");
            output.AddStyleAttribute("font-family", EventFontFamily);
            output.AddStyleAttribute("font-size", EventFontSize);
            output.AddStyleAttribute("cursor", "pointer");

            if (EventClickHandling == UserActionHandling.PostBack)
            {
                output.AddAttribute("onclick", "javascript:event.cancelBubble=true;" + Page.ClientScript.GetPostBackEventReference(this, "PK:" + p.PK));
            }
            else
            {
                output.AddAttribute("onclick", "javascript:event.cancelBubble=true;" + String.Format(EventClickJavaScript, p.PK));
            }

            output.RenderBeginTag("div");

            if (DurationBarVisible)
            {
                output.Write("<div unselectable='on' style='width:" + realWidth + "px; margin-left: " + startDelta + "px; height:2px; background-color:" + ColorTranslator.ToHtml(DurationBarColor) + "; font-size:1px; position:relative;' ></div>");
                output.Write("<div unselectable='on' style='width:" + width + "px; height:1px; background-color:" + ColorTranslator.ToHtml(EventBorderColor) + "; font-size:1px; position:relative;' ></div>");
            }

            output.AddStyleAttribute("display", "block");
            output.AddStyleAttribute("padding-left", "1px");
            output.AddAttribute("unselectable", "on");
            output.RenderBeginTag("div");
            output.Write(p.Name);
            output.RenderEndTag();
            output.RenderEndTag();
        }

        private void renderRows(HtmlTextWriter output)
        {
            foreach (Day d in days)
            {
                output.RenderBeginTag("tr");

                renderRowHeader(output, d);
                renderRowCells(output, d);

                output.RenderEndTag();

            }

        }

        private void renderRowCells(HtmlTextWriter output, Day d)
        {
            // render all events in the first cell
            output.AddStyleAttribute("width", "1px");
            output.AddStyleAttribute("border-bottom", "1px solid black");
            output.AddStyleAttribute("background-color", getCellColor(d.Start, d.Value));

            output.AddAttribute("valign", "top");
            output.AddAttribute("unselectable", "on");
            output.RenderBeginTag("td");

            renderEvents(d, output);

            // td
            output.RenderEndTag();

            renderCells(output, d);
        }

        private void renderRowHeader(HtmlTextWriter output, Day d)
        {
            int height = (d.MaxColumns() * EventHeight - 1);

            output.AddStyleAttribute("width", (RowHeaderWidth - 1) + "px");
            output.AddStyleAttribute("border-right", "1px solid " + ColorTranslator.ToHtml(BorderColor));
            output.AddStyleAttribute("border-left", "1px solid " + ColorTranslator.ToHtml(BorderColor));
            output.AddStyleAttribute("border-bottom", "1px solid " + ColorTranslator.ToHtml(BorderColor));
            output.AddStyleAttribute("background-color", ColorTranslator.ToHtml(HourNameBackColor));
            output.AddStyleAttribute("font-family", HeaderFontFamily);
            output.AddStyleAttribute("font-size", HeaderFontSize);
            output.AddStyleAttribute("color", ColorTranslator.ToHtml(HeaderFontColor));
            output.AddStyleAttribute("cursor", "default");

            output.AddAttribute("unselectable", "on");
            output.AddAttribute("resource", d.Value);
            output.RenderBeginTag("td");

            output.Write("<div unselectable='on' style='margin-left:4px; height:" + height + "px; line-height:" + height + "px; overflow:hidden;'>");
            output.Write(d.Name);
            output.Write("</div>");

            output.RenderEndTag();
        }

        private void renderCorner(HtmlTextWriter output)
        {

            output.AddStyleAttribute("width", (RowHeaderWidth - 1) + "px");
            output.AddStyleAttribute("height", (HeaderHeight - 1) + "px");
            output.AddStyleAttribute("border-right", "1px solid " + ColorTranslator.ToHtml(BorderColor));
            output.AddStyleAttribute("border-top", "1px solid " + ColorTranslator.ToHtml(BorderColor));
            output.AddStyleAttribute("border-left", "1px solid " + ColorTranslator.ToHtml(BorderColor));
            output.AddStyleAttribute("border-bottom", "1px solid " + ColorTranslator.ToHtml(BorderColor));
            output.AddStyleAttribute("background-color", ColorTranslator.ToHtml(HourNameBackColor));
            output.AddStyleAttribute("cursor", "default");
            output.AddAttribute("unselectable", "on");
            output.RenderBeginTag("div");

            output.RenderEndTag(); // td
        }

        internal void renderHeaderCols(HtmlTextWriter output)
        {
            for (int i = 0; i < cellCount; i++)
            {
                DateTime from = StartDate.AddMinutes(CellDuration * i);
                //DateTime to = from.AddMinutes(CellDuration);

                string text;

                if (CellDuration < 60) // smaller than hour, use minutes
                {
                    text = String.Format("<span style='color:gray'>{0:00}</span>", from.Minute);
                }
                else if (CellDuration < 1440)// smaller than day, use hours
                {
                    text = TimeFormatter.GetHour(from, TimeFormat, "{0} {1}");
                }
               else // use days
                {
                    text = from.Day.ToString();
                }

                if (i == 0)
                {
                    output.AddAttribute("colspan", "2");
                }
                if (i == cellCount - 1)
                {
                    output.AddStyleAttribute("border-right", "1px solid " + ColorTranslator.ToHtml(BorderColor));
                }
                output.AddStyleAttribute("border-top", "1px solid " + ColorTranslator.ToHtml(BorderColor));
                output.AddStyleAttribute("border-bottom", "1px solid " + ColorTranslator.ToHtml(BorderColor));
                output.AddStyleAttribute("width", (CellWidth) + "px");
                output.AddStyleAttribute("height", (HeaderHeight - 1) + "px");
                output.AddStyleAttribute("overflow", "hidden");
                output.AddStyleAttribute("text-align", "center");
                output.AddStyleAttribute("background-color", ColorTranslator.ToHtml(HourNameBackColor));
                output.AddStyleAttribute("font-family", HourFontFamily);
                output.AddStyleAttribute("font-size", HourFontSize);
                output.AddAttribute("unselectable", "on");
                output.AddStyleAttribute("-khtml-user-select", "none");
                output.AddStyleAttribute("-moz-user-select", "none");
                output.AddStyleAttribute("cursor", "default");
                output.RenderBeginTag("td");

                output.Write("<div unselectable='on' style='height:" + (HeaderHeight - 1) + "px;border-right: 1px solid " + ColorTranslator.ToHtml(HourNameBorderColor) + "; width:" + (CellWidth - 1) + "px;overflow:hidden;'>");
                output.Write(text);
                output.Write("</div>");
                output.RenderEndTag();
            }
        }

        private void loadEventsToDays()
        {
            days = new List<Day>();
            ArrayList items = (ArrayList)ViewState["Items"];

            if (Resources == null)
            {
                return;
            }

            foreach (Resource resource in Resources)
            {
                Day d = new Day(StartDate, EndDate.AddDays(1), resource.Name, resource.Value, CellDuration);
                days.Add(d);
            }

            foreach (Day d in days)
            {
                d.Load(items);
            }

        }


        #region Properties


        /// <summary>
        /// Gets or sets the first day to be shown. Default is DateTime.Today.
        /// </summary>
        [Description("The first day to be shown. Default is DateTime.Today.")]
        [Category("Behavior")]
        public DateTime StartDate
        {
            get
            {
                if (ViewState["StartDate"] == null)
                {
                    return DateTime.Today;
                }

                return (DateTime)ViewState["StartDate"];

            }
            set
            {
                ViewState["StartDate"] = new DateTime(value.Year, value.Month, value.Day);
            }
        }


        /// <summary>
        /// Gets the last day to be shown.
        /// </summary>
        [Browsable(false)]
        public DateTime EndDate
        {
            get
            {
                return StartDate.AddDays(Days - 1);
            }
        }


        /// <summary>
        /// Gets or sets the number of days to be displayed. Default is 1.
        /// </summary>
        [Description("The number of days to be displayed on the calendar. Default value is 1.")]
        [Category("Behavior")]
        [DefaultValue(1)]
        public int Days
        {
            get
            {
                if (ViewState["Days"] == null)
                    return 1;
                return (int)ViewState["Days"];
            }
            set
            {
                int daysCount = value;

                if (daysCount < 1)
                    daysCount = 1;

                ViewState["Days"] = daysCount;
            }
        }

        /// <summary>
        /// Cell size in minutes.
        /// </summary>
        [Description("Cell width in pixels.")]
        [Category("Layout")]
        [DefaultValue(20)]
        public virtual int CellWidth
        {
            get
            {
                if (ViewState["CellWidth"] == null)
                    return 20;
                return (int)ViewState["CellWidth"];
            }
            set
            {
                ViewState["CellWidth"] = value;
            }
        }

        /// <summary>
        /// Number of cells per hour horizontally.
        /// </summary>
        [Description("Cell length in minutes.")]
        [Category("Layout")]
        [DefaultValue(60)]
        public int CellDuration
        {
            get
            {
                if (ViewState["CellDuration"] == null)
                    return 60;
                return (int)ViewState["CellDuration"];
            }
            set
            {
                ViewState["CellDuration"] = value;
            }
        }



        /// <summary>
        /// Collection of rows (resources).
        /// </summary>
        [Category("Behavior")]
        [PersistenceMode(PersistenceMode.InnerProperty)]
        [Description("Collection of rows that will be used when ViewType property is set to ViewTypeEnum.Resources.")]
        public ResourceCollection Resources
        {
            get
            {
                if (ViewState["Resources"] == null)
                {
                    ResourceCollection rc = new ResourceCollection();
                    rc.designMode = DesignMode;

                    ViewState["Resources"] = rc;
                }
                return (ResourceCollection)ViewState["Resources"];
            }
        }

        /// <summary>
        /// Gets or sets the name of the column that contains the event starting date and time (must be convertible to DateTime).
        /// </summary>
        [Description("The name of the column that contains the event starting date and time (must be convertible to DateTime).")]
        [Category("Data")]
        public string DataStartField
        {
            get
            {
                return dataStartField;
            }
            set
            {
                dataStartField = value;

                if (Initialized)
                {
                    OnDataPropertyChanged();
                }

            }
        }

        /// <summary>
        /// Gets or sets the name of the column that contains the event ending date and time (must be convertible to DateTime).
        /// </summary>
        [Description("The name of the column that contains the event ending date and time (must be convertible to DateTime).")]
        [Category("Data")]
        public string DataEndField
        {
            get
            {
                return dataEndField;
            }
            set
            {
                dataEndField = value;
                if (Initialized)
                {
                    OnDataPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the  name of the column that contains the name of an event.
        /// </summary>
        [Category("Data")]
        [Description("The name of the column that contains the name of an event.")]
        public string DataTextField
        {
            get
            {
                return dataTextField;
            }
            set
            {
                dataTextField = value;

                if (Initialized)
                {
                    OnDataPropertyChanged();
                }

            }
        }

        /// <summary>
        /// Gets or sets the name of the column that contains the primary key. The primary key will be used for rendering the custom JavaScript actions.
        /// </summary>
        [Category("Data")]
        [Description("The name of the column that contains the primary key. The primary key will be used for rendering the custom JavaScript actions.")]
        public string DataValueField
        {
            get
            {
                return dataValueField;
            }
            set
            {
                dataValueField = value;

                if (Initialized)
                {
                    OnDataPropertyChanged();
                }

            }
        }

        /// <summary>
        /// Gets or sets the name of the column that contains the primary key. The primary key will be used for rendering the custom JavaScript actions.
        /// </summary>
        [Category("Data")]
        [Description("The name of the column that contains the column id.")]
        public string DataResourceField
        {
            get
            {
                return dataResourceField;
            }
            set
            {
                dataResourceField = value;

                if (Initialized)
                {
                    OnDataPropertyChanged();
                }

            }
        }

        /// <summary>
        /// Color of the hour names background.
        /// </summary>
        [Category("Appearance")]
        [TypeConverter(typeof(WebColorConverter))]
        [Description("Color of the hour names background.")]
        public Color HourNameBackColor
        {
            get
            {
                if (ViewState["HourNameBackColor"] == null)
                    return ColorTranslator.FromHtml("#ECE9D8");
                return (Color)ViewState["HourNameBackColor"];
            }
            set
            {
                ViewState["HourNameBackColor"] = value;
            }
        }

        /// <summary>
        /// Color of the horizontal border that separates our names.
        /// </summary>
        [Category("Appearance")]
        [TypeConverter(typeof(WebColorConverter))]
        [Description("Color of the horizontal border that separates our names.")]
        public Color HourNameBorderColor
        {
            get
            {
                if (ViewState["HourNameBorderColor"] == null)
                    return ColorTranslator.FromHtml("#ACA899");
                return (Color)ViewState["HourNameBorderColor"];
            }
            set
            {
                ViewState["HourNameBorderColor"] = value;
            }
        }



        /// <summary>
        /// Color of an event border.
        /// </summary>
        [Category("Appearance")]
        [TypeConverter(typeof(WebColorConverter))]
        [Description("Color of an event border.")]
        public Color EventBorderColor
        {
            get
            {
                if (ViewState["EventBorderColor"] == null)
                    return ColorTranslator.FromHtml("#000000");
                return (Color)ViewState["EventBorderColor"];
            }
            set
            {
                ViewState["EventBorderColor"] = value;
            }
        }

        /// <summary>
        /// Color of an event background.
        /// </summary>
        [Category("Appearance")]
        [TypeConverter(typeof(WebColorConverter))]
        [Description("Color of an event background.")]
        public Color EventBackColor
        {
            get
            {
                if (ViewState["EventBackColor"] == null)
                    return ColorTranslator.FromHtml("#FFFFFF");
                return (Color)ViewState["EventBackColor"];
            }
            set
            {
                ViewState["EventBackColor"] = value;
            }
        }


        ///<summary>
        ///Gets or sets the background color of the Web server control.
        ///</summary>
        ///
        ///<returns>
        ///A <see cref="T:System.Drawing.Color"></see> that represents the background color of the control. The default is <see cref="F:System.Drawing.Color.Empty"></see>, which indicates that this property is not set.
        ///</returns>
        ///
        public override Color BackColor
        {
            get
            {
                if (ViewState["BackColor"] == null)
                    return ColorTranslator.FromHtml("#FFFFD5");
                return (Color)ViewState["BackColor"];
            }
            set
            {
                ViewState["BackColor"] = value;
            }
        }


        /// <summary>
        /// Gets or sets the start of the business day (in hours).
        /// </summary>
        [Description("Start of the business day (hour from 0 to 23).")]
        [Category("Behavior")]
        [DefaultValue(9)]
        public int BusinessBeginsHour
        {
            get
            {
                if (ViewState["BusinessBeginsHour"] == null)
                    return 9;
                return (int)ViewState["BusinessBeginsHour"];
            }
            set
            {
                if (value < 0)
                    ViewState["BusinessBeginsHour"] = 0;
                else if (value > 23)
                    ViewState["BusinessBeginsHour"] = 23;
                else
                    ViewState["BusinessBeginsHour"] = value;
            }
        }


        /// <summary>
        /// Gets or sets the end of the business day (hours).
        /// </summary>
        [Description("End of the business day (hour from 1 to 24).")]
        [Category("Behavior")]
        [DefaultValue(18)]
        public int BusinessEndsHour
        {
            get
            {
                if (ViewState["BusinessEndsHour"] == null)
                    return 18;
                return (int)ViewState["BusinessEndsHour"];
            }
            set
            {
                if (value < BusinessBeginsHour)
                    ViewState["BusinessEndsHour"] = BusinessBeginsHour + 1;
                else if (value > 24)
                    ViewState["BusinessEndsHour"] = 24;
                else
                    ViewState["BusinessEndsHour"] = value;
            }
        }

        /// <summary>
        /// Font family of the row header, e.g. "Tahoma".
        /// </summary>
        [Description("Font family of the event text, e.g. \"Tahoma\".")]
        [Category("Appearance")]
        [DefaultValue("Tahoma")]
        public string HeaderFontFamily
        {
            get
            {
                if (ViewState["HeaderFontFamily"] == null)
                    return "Tahoma";

                return (string)ViewState["HeaderFontFamily"];
            }
            set
            {
                ViewState["HeaderFontFamily"] = value;
            }
        }

        /// <summary>
        /// Color of the column header font.
        /// </summary>
        [Description("Color of the column header font.")]
        [Category("Appearance")]
        public Color HeaderFontColor
        {
            get
            {
                if (ViewState["HeaderFontColor"] == null)
                    return ColorTranslator.FromHtml("#000000");

                return (Color)ViewState["HeaderFontColor"];
            }
            set
            {
                ViewState["HeaderFontColor"] = value;
            }
        }

        /// <summary>
        /// Font size of the row header, e.g. "10pt".
        /// </summary>
        [Description("Font size of the row header, e.g. \"10pt\".")]
        [Category("Appearance")]
        [DefaultValue("10pt")]
        public string HeaderFontSize
        {
            get
            {
                if (ViewState["HeaderFontSize"] == null)
                    return "10pt";

                return (string)ViewState["HeaderFontSize"];
            }
            set
            {
                ViewState["HeaderFontSize"] = value;
            }
        }


        /// <summary>
        /// Font family of the time axis headers, e.g. "Tahoma".
        /// </summary>
        [Category("Appearance")]
        [Description("Font family of the hour names (horizontal axis), e.g. \"Tahoma\".")]
        [DefaultValue("Tahoma")]
        public string HourFontFamily
        {
            get
            {
                if (ViewState["HourFontFamily"] == null)
                    return "Tahoma";

                return (string)ViewState["HourFontFamily"];
            }
            set
            {
                ViewState["HourFontFamily"] = value;
            }
        }

        /// <summary>
        /// Font size of the time axis header e.g. "16pt".
        /// </summary>
        [Description("Font size of the hour names (horizontal axis), e.g. \"10pt\".")]
        [Category("Appearance")]
        [DefaultValue("10pt")]
        public string HourFontSize
        {
            get
            {
                if (ViewState["HourFontSize"] == null)
                    return "10pt";

                return (string)ViewState["HourFontSize"];
            }
            set
            {
                ViewState["HourFontSize"] = value;
            }
        }

        /// <summary>
        /// Font family of the event text, e.g. "Tahoma".
        /// </summary>
        [Category("Appearance")]
        [DefaultValue("Tahoma")]
        [Description("Font family of the event text, e.g. \"Tahoma\".")]
        public string EventFontFamily
        {
            get
            {
                if (ViewState["EventFontFamily"] == null)
                    return "Tahoma";

                return (string)ViewState["EventFontFamily"];
            }
            set
            {
                ViewState["EventFontFamily"] = value;
            }
        }

        /// <summary>
        /// Font size of the event text, e.g. "8pt".
        /// </summary>
        [Category("Appearance")]
        [DefaultValue("7pt")]
        [Description("Font size of the event text, e.g. \"7pt\".")]
        public string EventFontSize
        {
            get
            {
                if (ViewState["EventFontSize"] == null)
                    return "7pt";

                return (string)ViewState["EventFontSize"];
            }
            set
            {
                ViewState["EventFontSize"] = value;
            }
        }

        /// <summary>
        /// Height of the event cell in pixels.
        /// </summary>
        [Category("Appearance")]
        [DefaultValue("17")]
        [Description("Height of the event cell in pixels.")]
        public int EventHeight
        {
            get
            {
                if (ViewState["EventHeight"] == null)
                    return 17;

                return (int)ViewState["EventHeight"];
            }
            set
            {
                ViewState["EventHeight"] = value;
            }
        }

        /// <summary>
        /// Height of the header cells (with hour names) in pixels.
        /// </summary>
        [Category("Appearance")]
        [DefaultValue("17")]
        [Description("Height of the header cells (with hour names) in pixels.")]
        public int HeaderHeight
        {
            get
            {
                if (ViewState["HeaderHeight"] == null)
                    return 17;

                return (int)ViewState["HeaderHeight"];
            }
            set
            {
                ViewState["HeaderHeight"] = value;
            }
        }

        /// <summary>
        /// Background color of time cells outside of the busines hours.
        /// </summary>
        [Category("Appearance")]
        [DefaultValue("#FFF4BC")]
        [TypeConverter(typeof(WebColorConverter))]
        [Description("Background color of time cells outside of the busines hours.")]
        public Color NonBusinessBackColor
        {
            get
            {
                if (ViewState["NonBusinessBackColor"] == null)
                    return ColorTranslator.FromHtml("#FFF4BC");

                return (Color)ViewState["NonBusinessBackColor"];
            }
            set
            {
                ViewState["NonBusinessBackColor"] = value;
            }
        }

        /// <summary>
        /// Color of the horizontal border that separates hour names.
        /// </summary>
        [Category("Appearance")]
        [TypeConverter(typeof(WebColorConverter))]
        [Description("Color of the vertical border that separates hour names.")]
        public Color HourBorderColor
        {
            get
            {
                if (ViewState["HourBorderColor"] == null)
                    return ColorTranslator.FromHtml("#EAD098");
                return (Color)ViewState["HourBorderColor"];

            }
            set
            {
                ViewState["HourBorderColor"] = value;
            }
        }

        ///<summary>
        ///Gets or sets the border color of the Web control.
        ///</summary>
        ///
        ///<returns>
        ///A <see cref="T:System.Drawing.Color"></see> that represents the border color of the control. The default is <see cref="F:System.Drawing.Color.Empty"></see>, which indicates that this property is not set.
        ///</returns>
        public override Color BorderColor
        {
            get
            {
                if (ViewState["BorderColor"] == null)
                    return ColorTranslator.FromHtml("#000000");
                return (Color)ViewState["BorderColor"];
            }
            set
            {
                ViewState["BorderColor"] = value;
            }
        }


        /// <summary>
        /// Color of the horizontal bar on the top of an event.
        /// </summary>
        [Category("Appearance")]
        [TypeConverter(typeof(WebColorConverter))]
        [Description("Color of the horizontal bar on the top of an event.")]
        public Color DurationBarColor
        {
            get
            {
                if (ViewState["DurationBarColor"] == null)
                    return ColorTranslator.FromHtml("blue");
                return (Color)ViewState["DurationBarColor"];
            }
            set
            {
                ViewState["DurationBarColor"] = value;
            }
        }


        /// <summary>
        /// Gets or sets the time-format for hour numbers (on the top).
        /// </summary>
        [Category("Appearance")]
        [Description("The time-format that will be used for the hour numbers.")]
        [DefaultValue(TimeFormat.Clock12Hours)]
        public TimeFormat TimeFormat
        {
            get
            {
                if (ViewState["TimeFormat"] == null)
                    return TimeFormat.Clock12Hours;
                return (TimeFormat)ViewState["TimeFormat"];
            }
            set
            {
                ViewState["TimeFormat"] = value;
            }
        }


        /// <summary>
        /// Width of the control (pixels and percentage values supported).
        /// </summary>
        [Description("Width of the control (pixels and percentage values supported).")]
        public new int Width
        {
            get
            {
                return RowHeaderWidth + cellCount * CellWidth;
            }
        }


        /// <summary>
        /// Width of the row header (resource names) in pixels.
        /// </summary>
        [Description("Width of the row header (resource names) in pixels.")]
        [Category("Appearance")]
        [DefaultValue(80)]
        public int RowHeaderWidth
        {
            get
            {
                if (ViewState["RowHeaderWidth"] == null)
                    return 80;
                return (int)ViewState["RowHeaderWidth"];
            }
            set
            {
                ViewState["RowHeaderWidth"] = value;
            }
        }

        /// <summary>
        /// Whether the color bar on the left side of and event should be visible.
        /// </summary>
        [Category("Appearance")]
        [DefaultValue(true)]
        [Description("Whether the duration bar on the top of and event should be visible.")]
        public bool DurationBarVisible
        {
            get
            {
                if (ViewState["DurationBarVisible"] == null)
                    return true;
                return (bool)ViewState["DurationBarVisible"];
            }
            set
            {
                ViewState["DurationBarVisible"] = value;
            }
        }

        /// <summary>
        /// Handling of user action (clicking an event).
        /// </summary>
        [Category("User actions")]
        [Description("Whether clicking an event should do a postback or run a javascript action. By default, it calls the javascript code specified in EventClickJavaScript property.")]
        [DefaultValue(UserActionHandling.JavaScript)]
        public UserActionHandling EventClickHandling
        {
            get
            {
                if (ViewState["EventClickHandling"] == null)
                    return UserActionHandling.JavaScript;
                return (UserActionHandling)ViewState["EventClickHandling"];
            }
            set
            {
                ViewState["EventClickHandling"] = value;
            }
        }

        /// <summary>
        /// Gets or sets the Javascript code that is executed when the users clicks on an event. '{0}' will be replaced by the primary key of the event.
        /// </summary>
        [Description("Javascript code that is executed when the users clicks on an event. '{0}' will be replaced by the primary key of the event.")]
        [Category("User actions")]
        [DefaultValue("alert('{0}');")]
        public string EventClickJavaScript
        {
            get
            {
                if (ViewState["EventClickJavaScript"] == null)
                    return "alert('{0}');";
                return (string)ViewState["EventClickJavaScript"];
            }
            set
            {
                ViewState["EventClickJavaScript"] = value;
            }
        }

        /// <summary>
        /// Handling of user action (clicking a free-time slot).
        /// </summary>
        [Category("User actions")]
        [Description("Whether clicking a free-time slot should do a postback or run a javascript action. By default, it calls the javascript code specified in FreeTimeClickJavaScript property.")]
        [DefaultValue(UserActionHandling.JavaScript)]
        public UserActionHandling FreeTimeClickHandling
        {
            get
            {
                if (ViewState["FreeTimeClickHandling"] == null)
                    return UserActionHandling.JavaScript;
                return (UserActionHandling)ViewState["FreeTimeClickHandling"];
            }
            set
            {
                ViewState["FreeTimeClickHandling"] = value;
            }
        }

        /// <summary>
        /// Gets or sets the Javascript code that is executed when the users clicks on a free time slot. '{0}' will be replaced by the starting time of that slot (i.e. '9:00'.
        /// </summary>
        [Description("Javascript code that is executed when the users clicks on a free time slot. '{0}' will be replaced by the starting time of that slot (i.e. '9:00'.")]
        [Category("User actions")]
        [DefaultValue("alert('{0}, {1}');")]
        public string FreeTimeClickJavaScript
        {
            get
            {
                if (ViewState["FreeTimeClickJavaScript"] == null)
                    return "alert('{0}, {1}');";
                return (string)ViewState["FreeTimeClickJavaScript"];
            }
            set
            {
                ViewState["FreeTimeClickJavaScript"] = value;
            }
        }

        [Category("Appearance")]
        [TypeConverter(typeof(WebColorConverter))]
        public Color HoverColor
        {
            get
            {
                if (ViewState["HoverColor"] == null)
                    return ColorTranslator.FromHtml("#FFED95");
                return (Color)ViewState["HoverColor"];

            }
            set
            {
                ViewState["HoverColor"] = value;
            }
        }


        #endregion

        #region Data binding

        ///<summary>
        ///Retrieves data from the associated data source.
        ///</summary>
        ///
        protected override void PerformSelect()
        {
            if (!IsBoundUsingDataSourceID)
            {
                this.OnDataBinding(EventArgs.Empty);
            }

            GetData().Select(CreateDataSourceSelectArguments(), this.OnDataSourceViewSelectCallback);

            RequiresDataBinding = false;
            MarkAsDataBound();

            OnDataBound(EventArgs.Empty);
        }

        private void OnDataSourceViewSelectCallback(IEnumerable retrievedData)
        {
            if (IsBoundUsingDataSourceID)
            {
                OnDataBinding(EventArgs.Empty);
            }
            PerformDataBinding(retrievedData);
        }


        ///<summary>
        ///Binds data from the data source to the control. 
        ///</summary>
        ///
        ///<param name="retrievedData">The <see cref="T:System.Collections.IEnumerable"></see> list of data returned from a <see cref="M:System.Web.UI.WebControls.DataBoundControl.PerformSelect"></see> method call.</param>
        protected override void PerformDataBinding(IEnumerable retrievedData)
        {
            ViewState["Items"] = new ArrayList();

            // don't load events in design mode
            if (DesignMode)
            {
                return;
            }

            base.PerformDataBinding(retrievedData);


            // Verify data exists.
            if (retrievedData == null)
            {
                return;
            }

            if (String.IsNullOrEmpty(DataStartField))
            {
                throw new NullReferenceException("DataStartField property must be specified.");
            }

            if (String.IsNullOrEmpty(DataEndField))
            {
                throw new NullReferenceException("DataEndField property must be specified.");
            }

            if (String.IsNullOrEmpty(DataTextField))
            {
                throw new NullReferenceException("DataTextField property must be specified.");
            }

            if (String.IsNullOrEmpty(DataValueField))
            {
                throw new NullReferenceException("DataValueField property must be specified.");
            }

            if (String.IsNullOrEmpty(DataResourceField))
            {
                throw new NullReferenceException("DataResourceField property must be specified.");
            }

            foreach (object dataItem in retrievedData)
            {

                DateTime start;
                DateTime end;

                string strStart = DataBinder.GetPropertyValue(dataItem, DataStartField, null);
                if (!DateTime.TryParse(strStart, out start))
                {
                    throw new FormatException(String.Format("Unable to convert '{0}' (from DataStartField column) to DateTime.", strStart));
                }

                string strEnd = DataBinder.GetPropertyValue(dataItem, DataEndField, null);
                if (!DateTime.TryParse(strEnd, out end))
                {
                    throw new FormatException(String.Format("Unable to convert '{0}' (from DataEndField column) to DateTime.", strEnd));
                }

                string name = DataBinder.GetPropertyValue(dataItem, DataTextField, null);
                string val = DataBinder.GetPropertyValue(dataItem, DataValueField, null);

                string resourceId = Convert.ToString(DataBinder.GetPropertyValue(dataItem, DataResourceField, null));

                ((ArrayList)ViewState["Items"]).Add(new Event(val, start, end, name, resourceId));

            }

            ((ArrayList)ViewState["Items"]).Sort(new EventComparer());

        }
        #endregion


        private int cellCount
        {
            get
            {
                return Days * 24 * 60 / CellDuration;
            }
        }

        internal string getCellColor(DateTime from, string resourceId)
        {
            bool isBusiness = isBusinessCell(from);

            if (isBusiness)
            {
                return ColorTranslator.ToHtml(BackColor);
            }
            else
            {
                return ColorTranslator.ToHtml(NonBusinessBackColor);
            }
        }

        private bool isBusinessCell(DateTime from)
        {
            if (from.DayOfWeek == DayOfWeek.Saturday || from.DayOfWeek == DayOfWeek.Sunday)
            {
                return false;
            }
            else
            {
                if (CellDuration < 720) // use hours
                {
                    if (from.Hour < BusinessBeginsHour || from.Hour >= BusinessEndsHour)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                else // use days
                {
                    return true;
                }
            }
        }

        ///<summary>
        ///When implemented by a class, enables a server control to process an event raised when a form is posted to the server.
        ///</summary>
        ///
        ///<param name="eventArgument">A <see cref="T:System.String"></see> that represents an optional event argument to be passed to the event handler. </param>
        public void RaisePostBackEvent(string eventArgument)
        {
            if (eventArgument.StartsWith("PK:"))
            {
                string pk = eventArgument.Substring(3, eventArgument.Length - 3);
                if (EventClick != null)
                    EventClick(this, new EventClickEventArgs(pk));
            }
            else if (eventArgument.StartsWith("TIME:"))
            {
                DateTime time = Convert.ToDateTime(eventArgument.Substring(5, 19));
                string resource = eventArgument.Substring(24);
                if (FreeTimeClick != null)
                    FreeTimeClick(this, new FreeClickEventArgs(time, resource));
            }
            else
            {
                throw new ArgumentException("Bad argument passed from postback event.");
            }

        }
    }
}
