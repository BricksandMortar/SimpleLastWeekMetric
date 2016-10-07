﻿// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Reporting.Dashboard;
using Rock.Web.Cache;
using Rock.Web.UI.Controls;

namespace Plugins.com_bricksandmortarstudio.Reporting
{
    /// <summary>
    /// NOTE: Most of the logic for processing the Attributes is in Rock.Rest.MetricsController.GetHtmlForBlock
    /// </summary>
    [DisplayName( "Last Week's Data Dashboard Widget" )]
    [Category( "Bricks and Mortar Studio > Dashboard" )]
    [Description( "Dashboard Widget from Liquid using YTD metric values" )]
    [EntityField( "Series Partition", "Select the series partition entity (Campus, Group, etc) to be used to limit the metric values for the selected metrics.", "Either select a specific {0} or leave {0} blank to get it from the page context.", Key = "Entity", Order = 3 )]
    [MetricCategoriesField( "Metric", "Select the metric(s) to be made available to liquid", Key = "MetricCategories", Order = 4 )]
    [BooleanField( "Round Values", "Round Y values to the nearest whole number. For example, display 25.00 as 25.", true, Order = 5 )]
    [CodeEditorField( "Liquid Template", "The text (or html) to display as a dashboard widget", CodeEditorMode.Lava, CodeEditorTheme.Rock, 200, Order = 6, DefaultValue =
@"
{% for metric in Metrics %}
    <h1>{{ metric.Title }}</h1>
    <h4>{{ metric.Subtitle }}</h4>
    <p>{{ metric.Description }}</p>
    <div class='row'>    
        <div class='col-md-6'>
            {{ metric.LastValueDate | Date: 'MMM' }}
              <span style='font-size:40px'>{{ metric.LastValue }}</span>
        </div>
        <div class='col-md-6'>
            <i class='{{ metric.IconCssClass }} fa-5x'></i>
        </div>
    </div>
{% endfor %}
" )]

    [BooleanField( "Enable Debug", "Outputs the object graph to help create your liquid syntax.", false, Order = 7 )]
    public partial class LastWeekDashboardWidget : DashboardWidget
    {



        public EntityTypeCache EntityType
        {
            get
            {
                string[] entityValues = ( GetAttributeValue( "Entity" ) ?? "" ).Split( '|' );
                if ( entityValues.Length != 2 || string.IsNullOrEmpty( entityValues[1] ) )
                {
                    return null;

                }
                var entityType = EntityTypeCache.Read( entityValues[0].AsGuid() );
                if ( entityType == null )
                {
                    return null;
                }
                else
                {
                    return entityType;
                }
            }
        }

        public int? EntityId
        {
            get
            {
                string[] entityValues = ( GetAttributeValue( "Entity" ) ?? "" ).Split( '|' );
                if ( entityValues.Length != 2 || string.IsNullOrEmpty( entityValues[1] ) )
                {
                    return null;

                }
                var entityType = EntityTypeCache.Read( entityValues[0].AsGuid() );
                if ( entityType == null )
                {
                    return null;
                }
                else
                {
                    return entityValues[1].AsIntegerOrNull();
                }
            }

        }


        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load" /> event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnLoad( System.EventArgs e )
        {
            base.OnLoad( e );
            pnlDashboardTitle.Visible = !string.IsNullOrEmpty( this.Title );
            pnlDashboardSubtitle.Visible = !string.IsNullOrEmpty( this.Subtitle );
            lDashboardTitle.Text = this.Title;
            lDashboardSubtitle.Text = this.Subtitle;
            lHtml.Text = GetHtml();
        }

        public string GetHtml()
        {
            RockContext rockContext = new RockContext();

            string lavaTemplate = GetAttributeValue( "LiquidTemplate" );

            var metricCategoryPairList = Rock.Attribute.MetricCategoriesFieldAttribute.GetValueAsGuidPairs( GetAttributeValue( "MetricCategories" ) );

            var metricGuids = metricCategoryPairList.Select( a => a.MetricGuid ).ToList();

            bool roundYValues = GetAttributeValue( "RoundValues" ).AsBooleanOrNull() ?? true;

            MetricService metricService = new MetricService( rockContext );
            var metrics = metricService.GetByGuids( metricGuids );
            List<object> metricsData = new List<object>();

            if ( metrics.Count() == 0 )
            {
                return @"<div class='alert alert-warning'> 
								Please select a metric in the block settings.
							</div>";
            }

            MetricValueService metricValueService = new MetricValueService( rockContext );
            
            DateTime currentDateTime = RockDateTime.Now;
            DateTime overAWeekAgo = RockDateTime.Now.AddDays( -8 );

            foreach ( var metric in metrics )
            {
                var metricSummary = JsonConvert.DeserializeObject( metric.ToJson(), typeof( MetricSummary ) ) as MetricSummary;
                var qryMeasureValues = metricValueService.Queryable()
                    .Where( a => a.MetricId == metric.Id && a.MetricValueType == MetricValueType.Measure && a.MetricValueDateTime > overAWeekAgo );

                //// if an entityTypeId/EntityId filter was specified, and the entityTypeId is the same as the metrics.EntityTypeId, filter the values to the specified entityId
                //// Note: if a Metric or it's Metric Value doesn't have a context, include it regardless of Context setting
                if ( EntityId.HasValue && ( metric.EntityTypeId == EntityId || metric.EntityTypeId == null ) )
                {
                    if ( EntityId.HasValue )
                    {
                        qryMeasureValues = qryMeasureValues.Where( a => a.EntityId == EntityId.Value || a.EntityId == null );
                    }
                }

                var lastMetricValue = qryMeasureValues.OrderByDescending( a => a.MetricValueDateTime ).FirstOrDefault();
                if ( lastMetricValue != null )
                {
                    metricSummary.LastValueDate = lastMetricValue.MetricValueDateTime.HasValue ? lastMetricValue.MetricValueDateTime.Value.Date : DateTime.MinValue;
                    metricSummary.LastValue = lastMetricValue.YValue.HasValue ? lastMetricValue.YValue.Value : ( decimal? ) null;
                }               

                metricsData.Add( metricSummary.ToLiquid() );
            }

            Rock.Lava.CommonMergeFieldsOptions lavaOptions = new Rock.Lava.CommonMergeFieldsOptions();
            Dictionary<string, object> mergeValues = Rock.Lava.LavaHelper.GetCommonMergeFields( null, null, lavaOptions );
            mergeValues.Add( "Metrics", metricsData );

            string resultHtml = lavaTemplate.ResolveMergeFields( mergeValues );

            // show liquid help for debug
            if ( GetAttributeValue( "EnableDebug" ).AsBoolean() )
            {
                resultHtml += mergeValues.lavaDebugInfo();
            }
            return resultHtml;
        }

    }

    public class MetricSummary : Metric
    {
        /// <summary>
        /// Gets or sets the last value.
        /// </summary>
        /// <value>
        /// The last value.
        /// </value>
        [DataMember]
        public object LastValue { get; set; }

        /// <summary>
        /// Gets or sets the last value date.
        /// </summary>
        /// <value>
        /// The last value date.
        /// </value>
        [DataMember]
        public DateTime LastValueDate { get; set; }

        /// <summary>
        /// Gets or sets the cumulative value.
        /// </summary>
        /// <value>
        /// The cumulative value.
        /// </value>
        [DataMember]
        public object CumulativeValue { get; set; }

        /// <summary>
        /// Gets or sets the goal value.
        /// </summary>
        /// <value>
        /// The goal value.
        /// </value>
        [DataMember]
        public object GoalValue { get; set; }
    }
}