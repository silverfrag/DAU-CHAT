import docx
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_ALIGN_VERTICAL
from docx.oxml import OxmlElement, parse_xml
from docx.oxml.ns import nsdecls, qn
import os

# Academic & Tech Dev XML Styling Helpers (Monochrome, Black & Neutral Slate)
def set_cell_background(cell, fill_hex):
    shading_elm = parse_xml(f'<w:shd {nsdecls("w")} w:fill="{fill_hex}"/>')
    cell._tc.get_or_add_tcPr().append(shading_elm)

def set_cell_margins(cell, top=100, bottom=100, left=140, right=140):
    tcPr = cell._tc.get_or_add_tcPr()
    tcMar = OxmlElement('w:tcMar')
    for m, val in [('w:top', top), ('w:bottom', bottom), ('w:left', left), ('w:right', right)]:
        node = OxmlElement(m)
        node.set(qn('w:w'), str(val))
        node.set(qn('w:type'), 'dxa')
        tcMar.append(node)
    tcPr.append(tcMar)

def set_table_borders(table, color="333333", sz="4", val="single"):
    tblPr = table._tbl.tblPr
    tblBorders = parse_xml(
        f'<w:tblBorders {nsdecls("w")}>'
        f'  <w:top w:val="{val}" w:sz="{sz}" w:space="0" w:color="{color}"/>'
        f'  <w:bottom w:val="{val}" w:sz="{sz}" w:space="0" w:color="{color}"/>'
        f'  <w:left w:val="{val}" w:sz="{sz}" w:space="0" w:color="{color}"/>'
        f'  <w:right w:val="{val}" w:sz="{sz}" w:space="0" w:color="{color}"/>'
        f'  <w:insideH w:val="{val}" w:sz="{sz}" w:space="0" w:color="D1D5DB"/>'
        f'  <w:insideV w:val="{val}" w:sz="{sz}" w:space="0" w:color="D1D5DB"/>'
        f'</w:tblBorders>'
    )
    tblPr.append(tblBorders)

def set_table_no_borders(table):
    tblPr = table._tbl.tblPr
    tblBorders = parse_xml(
        f'<w:tblBorders {nsdecls("w")}>'
        f'  <w:top w:val="none"/>'
        f'  <w:bottom w:val="none"/>'
        f'  <w:left w:val="none"/>'
        f'  <w:right w:val="none"/>'
        f'  <w:insideH w:val="none"/>'
        f'  <w:insideV w:val="none"/>'
        f'</w:tblBorders>'
    )
    tblPr.append(tblBorders)

def add_header_styled_row(table, headers, col_widths=None):
    hdr_cells = table.rows[0].cells
    for i, title in enumerate(headers):
        cell = hdr_cells[i]
        set_cell_background(cell, "EAEAEA")
        set_cell_margins(cell, top=120, bottom=120, left=140, right=140)
        p = cell.paragraphs[0]
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p.paragraph_format.space_before = Pt(2)
        p.paragraph_format.space_after = Pt(2)
        run = p.add_run(title)
        run.font.name = 'Times New Roman'
        run.font.size = Pt(11)
        run.font.bold = True
        run.font.color.rgb = RGBColor(0, 0, 0)
        if col_widths and i < len(col_widths):
            cell.width = col_widths[i]

def add_data_row(table, row_data, is_even=False, col_widths=None, alignments=None):
    row_cells = table.add_row().cells
    bg_color = "F9F9F9" if is_even else "FFFFFF"
    for i, val in enumerate(row_data):
        cell = row_cells[i]
        set_cell_background(cell, bg_color)
        set_cell_margins(cell, top=80, bottom=80, left=120, right=120)
        p = cell.paragraphs[0]
        p.paragraph_format.space_before = Pt(1)
        p.paragraph_format.space_after = Pt(1)
        p.paragraph_format.line_spacing = 1.15
        if alignments and i < len(alignments):
            p.alignment = alignments[i]
        else:
            p.alignment = WD_ALIGN_PARAGRAPH.LEFT
        run = p.add_run(str(val))
        run.font.name = 'Times New Roman'
        run.font.size = Pt(10.5)
        run.font.color.rgb = RGBColor(0, 0, 0)
        if col_widths and i < len(col_widths):
            cell.width = col_widths[i]

def add_body_p(doc, text):
    """Add a body text paragraph with JUSTIFY alignment and clean spacing without soft breaks"""
    lines = [l.strip() for l in text.split('\n') if l.strip()]
    last_p = None
    for line in lines:
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
        p.paragraph_format.space_before = Pt(0)
        p.paragraph_format.space_after = Pt(4)
        p.paragraph_format.line_spacing = 1.3
        run = p.add_run(line)
        run.font.name = 'Times New Roman'
        run.font.size = Pt(13)
        run.font.color.rgb = RGBColor(0, 0, 0)
        last_p = p
    return last_p

def add_bullet_p(doc, text, bold_prefix="", indent_inches=0.3):
    """Add a single bullet or numbered item as its OWN paragraph with LEFT alignment to prevent Word stretching bugs"""
    lines = [l.strip() for l in text.split('\n') if l.strip()]
    last_p = None
    for idx, line in enumerate(lines):
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.LEFT
        p.paragraph_format.left_indent = Inches(indent_inches)
        p.paragraph_format.space_before = Pt(0)
        p.paragraph_format.space_after = Pt(2.5)
        p.paragraph_format.line_spacing = 1.25
        if bold_prefix and idx == 0:
            r_b = p.add_run(bold_prefix + " ")
            r_b.font.name = 'Times New Roman'
            r_b.font.size = Pt(12.5)
            r_b.font.bold = True
            r_b.font.color.rgb = RGBColor(0, 0, 0)
        r_t = p.add_run(line)
        r_t.font.name = 'Times New Roman'
        r_t.font.size = Pt(12.5)
        r_t.font.color.rgb = RGBColor(0, 0, 0)
        last_p = p
    return last_p

def add_tech_box(doc, title, lines_list):
    """Academic / Tech Dev Code & Specification Block with individual lines (No stretched soft breaks)"""
    tbl = doc.add_table(rows=1, cols=1)
    tbl.alignment = WD_TABLE_ALIGNMENT.CENTER
    cell = tbl.cell(0, 0)
    set_cell_background(cell, "F8F9FA")
    set_cell_margins(cell, top=100, bottom=100, left=140, right=140)
    tcPr = cell._tc.get_or_add_tcPr()
    tcBorders = parse_xml(
        f'<w:tcBorders {nsdecls("w")}>'
        f'  <w:left w:val="single" w:sz="18" w:space="0" w:color="333333"/>'
        f'  <w:top w:val="single" w:sz="4" w:space="0" w:color="D1D5DB"/>'
        f'  <w:bottom w:val="single" w:sz="4" w:space="0" w:color="D1D5DB"/>'
        f'  <w:right w:val="single" w:sz="4" w:space="0" w:color="D1D5DB"/>'
        f'</w:tcBorders>'
    )
    tcPr.append(tcBorders)
    
    p = cell.paragraphs[0]
    p.alignment = WD_ALIGN_PARAGRAPH.LEFT
    p.paragraph_format.space_before = Pt(2)
    p.paragraph_format.space_after = Pt(3)
    run_t = p.add_run(title)
    run_t.font.name = 'Times New Roman'
    run_t.font.size = Pt(10.5)
    run_t.font.bold = True
    run_t.font.color.rgb = RGBColor(0, 0, 0)

    if isinstance(lines_list, str):
        lines = lines_list.split('\n')
    else:
        lines = lines_list

    for line in lines:
        p_line = cell.add_paragraph()
        p_line.alignment = WD_ALIGN_PARAGRAPH.LEFT
        p_line.paragraph_format.space_before = Pt(0)
        p_line.paragraph_format.space_after = Pt(1.5)
        p_line.paragraph_format.line_spacing = 1.15
        run_b = p_line.add_run(line)
        run_b.font.name = 'Consolas'
        run_b.font.size = Pt(9.5)
        run_b.font.color.rgb = RGBColor(30, 41, 59)

    p_after = doc.add_paragraph()
    p_after.paragraph_format.space_before = Pt(0)
    p_after.paragraph_format.space_after = Pt(4)

def add_figure(doc, img_path, caption, width=Inches(6.3)):
    if os.path.exists(img_path):
        p_img = doc.add_paragraph()
        p_img.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p_img.paragraph_format.space_before = Pt(8)
        p_img.paragraph_format.space_after = Pt(4)
        run_img = p_img.add_run()
        run_img.add_picture(img_path, width=width)
        
        if caption:
            p_cap = doc.add_paragraph()
            p_cap.alignment = WD_ALIGN_PARAGRAPH.CENTER
            p_cap.paragraph_format.space_before = Pt(0)
            p_cap.paragraph_format.space_after = Pt(10)
            run_cap = p_cap.add_run(caption)
            run_cap.font.name = 'Times New Roman'
            run_cap.font.size = Pt(10.5)
            run_cap.font.bold = True
            run_cap.font.italic = True
            run_cap.font.color.rgb = RGBColor(0, 0, 0)

print("Tech academic docx helpers updated with zero-stretching paragraph models.")
