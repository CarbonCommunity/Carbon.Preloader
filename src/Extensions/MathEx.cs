using System;

/*
 *
 * Copyright (c) 2022-2024 Carbon Community
 * All rights reserved.
 *
 */

namespace Carbon.Extensions;

public static class MathEx
{
	public static float Percentage(this int value, int total, float percent = 100)
	{
		return (float)Math.Round((double)percent * value) / total;
	}
}
