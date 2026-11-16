import docx
from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_ALIGN_VERTICAL
from docx.oxml import OxmlElement, parse_xml
from docx.oxml.ns import nsdecls, qn
import os

def create_report():
    doc = Document()

    # 1. Page Setup - A4, Standard Vietnamese Thesis Margins: Top 2cm, Bottom 2cm, Left 3cm, Right 2cm
    for section in doc.sections:
        section.top_margin = Inches(0.79)     # ~2.0 cm
        section.bottom_margin = Inches(0.79)  # ~2.0 cm
        section.left_margin = Inches(1.18)    # ~3.0 cm
        section.right_margin = Inches(0.79)   # ~2.0 cm
        section.page_width = Inches(8.27)     # 21.0 cm (A4)
        section.page_height = Inches(11.69)   # 29.7 cm (A4)

    # 2. Setup Styles
    styles = doc.styles

    # Normal Style
    style_normal = styles['Normal']
    font = style_normal.font
    font.name = 'Times New Roman'
    font.size = Pt(13)
    font.color.rgb = RGBColor(30, 41, 59) # Slate 800
    style_normal.paragraph_format.line_spacing = 1.3
    style_normal.paragraph_format.space_after = Pt(4)
    style_normal.paragraph_format.space_before = Pt(0)

    # Heading 1
    style_h1 = styles['Heading 1']
    h1_font = style_h1.font
    h1_font.name = 'Times New Roman'
    h1_font.size = Pt(16)
    h1_font.bold = True
    h1_font.color.rgb = RGBColor(15, 45, 89) # Deep Navy
    style_h1.paragraph_format.space_before = Pt(14)
    style_h1.paragraph_format.space_after = Pt(6)
    style_h1.paragraph_format.keep_with_next = True

    # Heading 2
    style_h2 = styles['Heading 2']
    h2_font = style_h2.font
    h2_font.name = 'Times New Roman'
    h2_font.size = Pt(14)
    h2_font.bold = True
    h2_font.color.rgb = RGBColor(27, 77, 137) # Classic Navy
    style_h2.paragraph_format.space_before = Pt(10)
    style_h2.paragraph_format.space_after = Pt(4)
    style_h2.paragraph_format.keep_with_next = True

    # Heading 3
    style_h3 = styles['Heading 3']
    h3_font = style_h3.font
    h3_font.name = 'Times New Roman'
    h3_font.size = Pt(13)
    h3_font.bold = True
    h3_font.italic = True
    h3_font.color.rgb = RGBColor(27, 77, 137)
    style_h3.paragraph_format.space_before = Pt(6)
    style_h3.paragraph_format.space_after = Pt(2)
    style_h3.paragraph_format.keep_with_next = True

    return doc

print("Setup template function ready")
