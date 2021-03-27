﻿// The MIT License(MIT)

// Copyright(c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using LiveChartsCore.Drawing;
using LiveChartsCore.Drawing.Common;
using LiveChartsCore.Measure;
using System;
using System.Collections.Generic;

namespace LiveChartsCore.Kernel
{
    /// <summary>
    /// Defines a series a chart series that has a visual representation in the user interface.
    /// </summary>
    /// <typeparam name="TDrawingContext">The type of the drawing context.</typeparam>
    /// <seealso cref="LiveChartsCore.ISeries" />
    public interface IDrawableSeries<TDrawingContext> : ISeries
         where TDrawingContext : DrawingContext
    {
        /// <summary>
        /// Gets or sets the stroke drawable task.
        /// </summary>
        /// <value>
        /// The stroke.
        /// </value>
        IDrawableTask<TDrawingContext>? Stroke { get; set; }

        /// <summary>
        /// Gets or sets the fill  drawable task.
        /// </summary>
        /// <value>
        /// The fill.
        /// </value>
        IDrawableTask<TDrawingContext>? Fill { get; set; }

        /// <summary>
        /// Gets or sets the data labels  drawable task.
        /// </summary>
        IDrawableTask<TDrawingContext>? DataLabelsDrawableTask { get; set; }

        /// <summary>
        /// Gets or sets the size of the data labels.
        /// </summary>
        /// <value>
        /// The size of the data labels.
        /// </value>
        double DataLabelsSize { get; set; }

        /// <summary>
        /// Gets or sets the data labels position.
        /// </summary>
        /// <value>
        /// The data labels position.
        /// </value>
        DataLabelsPosition DataLabelsPosition { get; set; }

        /// <summary>
        /// Gets or sets the data labels padding.
        /// </summary>
        /// <value>
        /// The data labels padding.
        /// </value>
        Padding DataLabelsPadding { get; set; }

        /// <summary>
        /// Gets or sets the data label formatter, it is a delegate that takes the point as parameter, and returns the label as string.
        /// </summary>
        /// <value>
        /// The data label formatter.
        /// </value>
        Func<ChartPoint, string> DataLabelFormatter { get; set; }

        /// <summary>
        /// Gets the default paint context, normally handled internally to display tooltips and legends.
        /// </summary>
        /// <value>
        /// The default paint context.
        /// </value>
        PaintContext<TDrawingContext> DefaultPaintContext { get; }

        /// <summary>
        /// Gets the stack group, normally used internally to handled the stacked series.
        /// </summary>
        /// <returns></returns>
        int GetStackGroup();

        /// <summary>
        /// Gets the deleting tasks.
        /// </summary>
        /// <value>
        /// The deleting tasks.
        /// </value>
        List<IDrawableTask<TDrawingContext>> DeletingTasks { get; }
    }
}