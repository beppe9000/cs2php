﻿using System;

namespace Lang.Php
{

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public class DefaultTimezoneAttribute : Attribute
    {
        public DefaultTimezoneAttribute(Timezones Timezone)
        {
            this.Timezone = Timezone;
        }
        public Timezones Timezone { get; private set; }
    }

}
