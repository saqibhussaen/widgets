using System.Windows;
using System.Windows.Markup;

namespace ClockWidget;

// Original WPF templates, MIT; no external theme library or artwork.
internal static class SettingsTheme
{
    public static ResourceDictionary Create() => (ResourceDictionary)XamlReader.Parse("""
    <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
      <SolidColorBrush x:Key="Accent" Color="#B7A1FF"/>
      <Style TargetType="Button">
        <Setter Property="Foreground" Value="#F4F1FF"/><Setter Property="Background" Value="#262833"/>
        <Setter Property="BorderBrush" Value="#363845"/><Setter Property="BorderThickness" Value="1"/>
        <Setter Property="Padding" Value="14,9"/><Setter Property="Cursor" Value="Hand"/><Setter Property="FontWeight" Value="Medium"/>
        <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
          <Border x:Name="Surface" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="9" Padding="{TemplateBinding Padding}">
            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Surface" Property="Opacity" Value="0.8"/></Trigger>
            <Trigger Property="IsPressed" Value="True"><Setter TargetName="Surface" Property="Opacity" Value="0.6"/></Trigger>
            <Trigger Property="IsEnabled" Value="False"><Setter TargetName="Surface" Property="Opacity" Value="0.4"/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate></Setter.Value></Setter>
      </Style>
      <Style x:Key="PrimaryButton" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
        <Setter Property="Background" Value="{StaticResource Accent}"/><Setter Property="Foreground" Value="#1A142B"/><Setter Property="BorderBrush" Value="{StaticResource Accent}"/><Setter Property="FontWeight" Value="SemiBold"/>
      </Style>
      <Style TargetType="RadioButton">
        <Setter Property="Foreground" Value="#B7B6C6"/><Setter Property="Cursor" Value="Hand"/><Setter Property="Padding" Value="12,9"/><Setter Property="Margin" Value="0,0,5,5"/>
        <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="RadioButton">
          <Border x:Name="Surface" Background="#232530" BorderBrush="#323441" BorderThickness="1" CornerRadius="8" Padding="{TemplateBinding Padding}"><ContentPresenter VerticalAlignment="Center" HorizontalAlignment="Center"/></Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsChecked" Value="True"><Setter TargetName="Surface" Property="Background" Value="#363047"/><Setter TargetName="Surface" Property="BorderBrush" Value="#B7A1FF"/><Setter Property="Foreground" Value="#E6DDFF"/></Trigger>
            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Surface" Property="Opacity" Value="0.8"/></Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Surface" Property="BorderBrush" Value="White"/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate></Setter.Value></Setter>
      </Style>
      <Style TargetType="CheckBox">
        <Setter Property="Foreground" Value="#E1DFEB"/><Setter Property="Cursor" Value="Hand"/>
        <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="CheckBox">
          <Grid MinHeight="28"><Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width="42"/></Grid.ColumnDefinitions>
            <ContentPresenter VerticalAlignment="Center" Margin="0,0,16,0"/>
            <Border x:Name="Pill" Grid.Column="1" Background="#3A3D4B" CornerRadius="11" Width="38" Height="22" VerticalAlignment="Center">
              <Ellipse x:Name="Knob" Fill="#D2CFDE" Width="16" Height="16" HorizontalAlignment="Left" Margin="3"/>
            </Border>
          </Grid>
          <ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="Pill" Property="Background" Value="#B7A1FF"/><Setter TargetName="Knob" Property="Fill" Value="#241C38"/><Setter TargetName="Knob" Property="HorizontalAlignment" Value="Right"/></Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Pill" Property="BorderBrush" Value="White"/><Setter TargetName="Pill" Property="BorderThickness" Value="1"/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate></Setter.Value></Setter>
      </Style>
      <Style TargetType="Slider">
        <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Slider"><Grid Height="28" Background="Transparent">
          <Track x:Name="PART_Track" Minimum="{TemplateBinding Minimum}" Maximum="{TemplateBinding Maximum}" Value="{TemplateBinding Value}">
            <Track.DecreaseRepeatButton><RepeatButton Command="Slider.DecreaseLarge" Focusable="False"><RepeatButton.Template><ControlTemplate><Border Height="4" CornerRadius="2" Background="#B7A1FF"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>
            <Track.IncreaseRepeatButton><RepeatButton Command="Slider.IncreaseLarge" Focusable="False"><RepeatButton.Template><ControlTemplate><Border Height="4" CornerRadius="2" Background="#363845"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>
            <Track.Thumb><Thumb Width="18" Height="18"><Thumb.Template><ControlTemplate><Ellipse Fill="#DED3FF" Stroke="#B7A1FF" StrokeThickness="3"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
          </Track>
        </Grid></ControlTemplate></Setter.Value></Setter>
      </Style>
      <Style TargetType="TextBox">
        <Setter Property="Foreground" Value="#F4F1FF"/><Setter Property="CaretBrush" Value="#B7A1FF"/><Setter Property="Background" Value="#20222C"/><Setter Property="BorderBrush" Value="#3A3C48"/><Setter Property="BorderThickness" Value="1"/>
        <Setter Property="SelectionBrush" Value="#645184"/>
        <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TextBox"><Border x:Name="Surface" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1" CornerRadius="8" Padding="{TemplateBinding Padding}"><ScrollViewer x:Name="PART_ContentHost"/></Border>
          <ControlTemplate.Triggers><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Surface" Property="BorderBrush" Value="#B7A1FF"/></Trigger></ControlTemplate.Triggers>
        </ControlTemplate></Setter.Value></Setter>
      </Style>
      <Style TargetType="TabControl">
        <Setter Property="Background" Value="Transparent"/>
        <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TabControl"><Grid>
          <Grid.ColumnDefinitions><ColumnDefinition Width="154"/><ColumnDefinition/></Grid.ColumnDefinitions>
          <Border BorderBrush="#292B35" BorderThickness="0,0,1,0" Margin="0,0,12,0"><TabPanel IsItemsHost="True" Margin="0,8,8,0" VerticalAlignment="Top"/></Border>
          <ContentPresenter x:Name="PART_SelectedContentHost" Grid.Column="1" ContentSource="SelectedContent"/>
        </Grid></ControlTemplate></Setter.Value></Setter>
      </Style>
      <Style TargetType="TabItem">
        <Setter Property="Foreground" Value="#9998AB"/><Setter Property="Cursor" Value="Hand"/>
        <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TabItem">
          <Border x:Name="Surface" CornerRadius="9" Margin="0,0,0,6" Padding="16,12" Background="Transparent"><ContentPresenter ContentSource="Header"/></Border>
          <ControlTemplate.Triggers><Trigger Property="IsSelected" Value="True"><Setter TargetName="Surface" Property="Background" Value="#30293F"/><Setter Property="Foreground" Value="#DCCEFF"/><Setter Property="FontWeight" Value="SemiBold"/></Trigger>
          <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Surface" Property="Opacity" Value="0.75"/></Trigger></ControlTemplate.Triggers>
        </ControlTemplate></Setter.Value></Setter>
      </Style>
      <Style TargetType="ScrollBar"><Setter Property="Width" Value="6"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ScrollBar"><Track x:Name="PART_Track" Orientation="{TemplateBinding Orientation}" Minimum="{TemplateBinding Minimum}" Maximum="{TemplateBinding Maximum}" Value="{TemplateBinding Value}" ViewportSize="{TemplateBinding ViewportSize}" IsDirectionReversed="True"><Track.Thumb><Thumb><Thumb.Template><ControlTemplate><Border Background="#3C3C4B" CornerRadius="3"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb></Track></ControlTemplate></Setter.Value></Setter></Style>
      <Style TargetType="ListBox"><Setter Property="Background" Value="#20222C"/><Setter Property="Foreground" Value="#F2F0F8"/><Setter Property="BorderBrush" Value="#3A3C48"/></Style>
      <Style TargetType="ListBoxItem"><Setter Property="Padding" Value="9"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ListBoxItem"><Border x:Name="Surface" Background="Transparent" Padding="{TemplateBinding Padding}"><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property="IsSelected" Value="True"><Setter TargetName="Surface" Property="Background" Value="#453655"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
    </ResourceDictionary>
    """);
}
