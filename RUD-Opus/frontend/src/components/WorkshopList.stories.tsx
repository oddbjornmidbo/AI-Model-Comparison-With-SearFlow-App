import type { Meta, StoryObj } from '@storybook/react-vite'
import { workshops } from '../fixtures'
import { WorkshopList } from './WorkshopList'

const meta = {
  title: 'Components/WorkshopList',
  component: WorkshopList,
  args: { workshops, selectedId: null, onSelect: () => {} },
} satisfies Meta<typeof WorkshopList>
export default meta
type Story = StoryObj<typeof meta>

export const Populated: Story = {}
export const WithSelection: Story = { args: { selectedId: 2 } }
export const Loading: Story = { args: { workshops: undefined, loading: true } }
export const Empty: Story = { args: { workshops: [] } }
export const Error: Story = { args: { workshops: undefined, error: 'Could not reach the server.', onRetry: () => {} } }
